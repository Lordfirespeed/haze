using System.Threading;
using System.Threading.Tasks;
using Haze.Mvc;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Haze.Controllers;

[ApiController]
public class WebSocketController : HazeControllerBase<WebSocketController>
{

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
        HazeC2SMessage? message;

        while (true)
        {
            message = await webSocket.ReceiveMessage(ct);
            if (message is null) return;
            _logger.LogInformation("is it an auth message: {}", message is HazeC2SAuthenticateMessage);
        }
    }
}
