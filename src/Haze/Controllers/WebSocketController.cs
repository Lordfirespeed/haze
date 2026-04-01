using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Haze.MessageHandlers;
using Haze.Mvc;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskExtensions = Haze.Util.TaskExtensions;

namespace Haze.Controllers;

[ApiController]
public class WebSocketController : HazeControllerBase<WebSocketController>
{
    private static readonly Dictionary<Type, IHazeC2SMessageHandler> HandlerCache = new();
    private static readonly IHazeC2SMessageHandler[] Handlers =
    [
        new HazeC2SAuthenticateHandler(),
    ];

    protected static HazeC2SMessageHandler<TMessage> GetHandler<TMessage>() where TMessage : HazeC2SMessage
    {
        var handler = GetHandler(typeof(TMessage));
        Debug.Assert(handler is HazeC2SMessageHandler<TMessage>);
        return (HazeC2SMessageHandler<TMessage>)handler;
    }

    protected static IHazeC2SMessageHandler GetHandler(Type messageType)
    {
        if (HandlerCache.TryGetValue(messageType, out var cachedHandler)) return cachedHandler;
        foreach (var handler in Handlers) {
            if (!handler.CanHandle(messageType)) continue;
            HandlerCache.Add(messageType, handler);
            return handler;
        }
        throw new KeyNotFoundException($"No suitable handler registered for {messageType.Name}");
    }

    private static readonly BoundedChannelOptions ChannelOptions = new(8)
    {
        FullMode = BoundedChannelFullMode.Wait,
    };

    public WebSocketController(ILogger<WebSocketController> logger) : base(logger) { }

    [Route("/api/websocket")]
    [HttpGet, HttpConnect]
    public async Task<IActionResult> Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return Problem("Only websocket requests are allowed", statusCode: StatusCodes.Status400BadRequest);

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var hazeWebSocket = new HazeWebSocket(webSocket, _logger);
        await Handle(hazeWebSocket);
        return Empty;
    }

    private async Task Handle(HazeWebSocket webSocket, CancellationToken ct = default)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var queue = Channel.CreateBounded<HazeS2CMessage>(ChannelOptions);

        var receiveLoopTask = Task.Run(() => ReceiveLoop(webSocket, queue, cts.Token));
        var sendLoopTask = Task.Run(() => SendLoop(webSocket, queue, cts.Token));

        try {
            await TaskExtensions.Group([receiveLoopTask, sendLoopTask], cts);
        } catch (WebSocketException exc) {
            _logger.LogDebug(exc, "WebSocket exception occurred");
        } catch (OperationCanceledException exc) {
            _logger.LogDebug(exc, "WebSocket closed and handling cancelled gracefully");
        }
    }

    private async Task ReceiveLoop(HazeWebSocket webSocket, ChannelWriter<HazeS2CMessage> messageQueue, CancellationToken ct = default)
    {
        while (true) {
            ct.ThrowIfCancellationRequested();
            var message = await webSocket.ReceiveMessage(ct);
            _logger.LogInformation("is it an auth message: {}", message is HazeC2SAuthenticateMessage);
            IHazeC2SMessageHandler handler;
            try {
                handler = GetHandler(message.GetType());
            } catch (KeyNotFoundException exc) {
                _logger.LogWarning(exc, "Missing handler");
                continue;
            }
            await handler.Handle(webSocket, message, ct);
        }
    }

    private async Task SendLoop(HazeWebSocket webSocket, ChannelReader<HazeS2CMessage> messageQueue, CancellationToken ct = default)
    {
        while (true) {
            ct.ThrowIfCancellationRequested();
            var message = await messageQueue.ReadAsync(ct);
            await webSocket.SendMessage(message, ct);
        }
    }
}
