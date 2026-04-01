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

    protected ILogger Logger { get; }

    public WebSocket WebSocket { get; }

    public HazeClientSession? ClientSession { get; set; }

    public async Task<HazeC2SMessage?> ReceiveMessage(CancellationToken ct = default)
    {
        var messageStream = await ReceiveRawMessage(ct);
        if (messageStream is null) return null;

        HazeC2SMessage? message;
        try {
            message = await JsonSerializer.DeserializeAsync<HazeC2SMessage>(messageStream, MessageSerializeOptions, ct);
        }
        catch (JsonException exception)
        {
            await using var sendScope = await _sendLock.EnterScope(CancellationToken.None);
            Logger.LogDebug(exception, "Closing connection as message contained malformed JSON contents");
            await WebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Only messages of type 'text' containing a single JSON-encoded object are acceptable", CancellationToken.None);
            return null;
        }

        if (message is null || message.GetType() == typeof(HazeC2SMessage))
        {
            await using var sendScope = await _sendLock.EnterScope(CancellationToken.None);
            Logger.LogDebug("Closing connection as message is empty");
            await WebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Empty messages (i.e. 'null', '{}') are not acceptable", CancellationToken.None);
            return null;
        }

        return message;
    }

    public async Task<MemoryStream?> ReceiveRawMessage(CancellationToken ct = default)
    {
        await using var receiveScope = await _receiveLock.EnterScope(ct);
        var buffer = new byte[1024 * 4];
        int totalReceived = 0;
        while (true)
        {
            if (totalReceived >= buffer.Length)
            {
                await using var sendScope = await _sendLock.EnterScope(CancellationToken.None);
                await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Single message too large - maximum allowed size 4KiB", CancellationToken.None);
                return null;
            }
            var receiveResult = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer, totalReceived, buffer.Length - totalReceived), ct);
            totalReceived += receiveResult.Count;
            if (receiveResult.CloseStatus.HasValue)
            {
                await using var sendScope = await _sendLock.EnterScope(CancellationToken.None);
                await WebSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None);
                return null;
            }
            if (receiveResult.MessageType is not WebSocketMessageType.Text)
            {
                await using var sendScope = await _sendLock.EnterScope(CancellationToken.None);
                await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Only messages of type 'text' containing JSON data are acceptable", CancellationToken.None);
                return null;
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
}
