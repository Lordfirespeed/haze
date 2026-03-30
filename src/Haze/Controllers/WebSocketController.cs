using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Haze.Messages;
using Haze.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Controllers;

[ApiController]
public class WebSocketController : HazeControllerBase<WebSocketController>
{
    private static readonly JsonSerializerOptions MessageSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WebSocketController(ILogger<WebSocketController> logger) : base(logger) { }

    [Route("/api/websocket")]
    [HttpGet, HttpConnect]
    public async Task<IActionResult> Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return Problem("Only websocket requests are allowed", statusCode: StatusCodes.Status400BadRequest);

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await Handle(webSocket);
        return Empty;
    }

    private async Task Handle(WebSocket webSocket, CancellationToken ct = default)
    {
        HazeMessage? message;

        while (true)
        {
            message = await ReceiveMessage(webSocket, ct);
            if (message is null) return;
            _logger.LogInformation("is it an auth message: {}", message is HazeAuthenticateMessage);
        }
    }

    private async Task<HazeMessage?> ReceiveMessage(WebSocket webSocket, CancellationToken ct = default)
    {
        var buffer = new byte[1024 * 4];
        int totalReceived = 0;
        while (true)
        {
            if (totalReceived >= buffer.Length)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Single message too large - maximum allowed size 4KiB", ct);
                return null;
            }
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, totalReceived, buffer.Length - totalReceived), ct);
            totalReceived += receiveResult.Count;
            if (receiveResult.CloseStatus.HasValue)
            {
                await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, ct);
                return null;
            }
            if (receiveResult.MessageType is not WebSocketMessageType.Text)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Only messages of type 'text' containing JSON data are acceptable", ct);
                return null;
            }

            if (receiveResult.EndOfMessage) break;
        }

        HazeMessage? message = null;
        try {
            var messageStream = new MemoryStream(buffer, 0, totalReceived);
            message = await JsonSerializer.DeserializeAsync<HazeMessage>(messageStream, MessageSerializeOptions, ct);
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "Closing connection as message contained malformed JSON contents");
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Only messages of type 'text' containing JSON data are acceptable", ct);
        }
        return message;
    }
}
