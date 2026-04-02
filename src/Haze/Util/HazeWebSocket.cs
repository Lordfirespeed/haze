using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.Util;

public class HazeWebSocket
{
    private static readonly JsonSerializerOptions MessageSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly AsyncLock _receiveLock = new();
    private readonly AsyncLock _sendLock = new();

    public HazeWebSocket(WebSocket webSocket, ILogger logger)
    {
        WebSocket = webSocket;
        Logger = logger;
    }

    public ILogger Logger { get; }

    public WebSocket WebSocket { get; }

    public HazeClientSession? ClientSession { get; set; }

    public async Task<HazeC2SMessage> ReceiveMessage(CancellationToken ct = default)
    {
        var messageStream = await ReceiveRawMessage(ct);

        HazeC2SMessage? message;
        try {
            message = await JsonSerializer.DeserializeAsync<HazeC2SMessage>(messageStream, MessageSerializeOptions, ct);
        }
        catch (JsonException exception)
        {
            await Close(WebSocketCloseStatus.PolicyViolation, "Only messages of type 'text' containing a single JSON-encoded object are acceptable", CancellationToken.None);
            throw new OperationCanceledException("Connection closed; received a message with malformed JSON contents", exception);
        }

        if (message is null || message.GetType() == typeof(HazeC2SMessage))
        {
            await Close(WebSocketCloseStatus.PolicyViolation, "Empty messages (i.e. 'null', '{}') are not acceptable", CancellationToken.None);
            throw new OperationCanceledException("Connection closed; received a valid JSON-encoded message, but it was empty or null");
        }

        return message;
    }

    public async Task<MemoryStream> ReceiveRawMessage(CancellationToken ct = default)
    {
        await using var receiveScope = await _receiveLock.EnterScope(ct);
        var buffer = new byte[1024 * 4];
        int totalReceived = 0;
        while (true)
        {
            if (totalReceived >= buffer.Length)
            {
                await Close(WebSocketCloseStatus.MessageTooBig, "Single message too large - maximum allowed size 4KiB", CancellationToken.None);
                throw new OperationCanceledException("Connection closed; received message exceeding allowed size");
            }
            var receiveResult = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer, totalReceived, buffer.Length - totalReceived), ct);
            totalReceived += receiveResult.Count;
            if (receiveResult.CloseStatus.HasValue)
            {
                await Close(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None);
                throw new OperationCanceledException("Connection closed; remote party completed the close handshake and closed the WebSocket connection");
            }
            if (receiveResult.MessageType is not WebSocketMessageType.Text)
            {
                await Close(WebSocketCloseStatus.InvalidMessageType, "Only messages of type 'text' containing JSON data are acceptable", CancellationToken.None);
                throw new OperationCanceledException("Connection closed; received message with non-text contents");
            }

            if (receiveResult.EndOfMessage) break;
        }

        return new MemoryStream(buffer, 0, totalReceived);
    }

    public async Task SendMessage(HazeS2CMessage message, CancellationToken ct = default)
    {
        await using var sendScope = await _sendLock.EnterScope(ct);
        await WebSocket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, MessageSerializeOptions), WebSocketMessageType.Text, true, ct);
    }

    public async Task Close(
        WebSocketCloseStatus closeStatus,
        string? closeStatusDetail,
        CancellationToken ct = default
    ) {
        await using var sendScope = await _sendLock.EnterScope(ct);
        await WebSocket.CloseAsync(closeStatus, closeStatusDetail, ct);
    }
}
