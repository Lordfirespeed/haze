using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SAuthenticateHandler : HazeC2SMessageHandler<HazeC2SAuthenticateMessage>
{
    public HazeC2SAuthenticateHandler(HazeDbContext dbContext) : base(dbContext) { }

    public override Task Handle(HazeWebSocket webSocket, HazeC2SAuthenticateMessage message, CancellationToken ct = default)
    {
        webSocket.Logger.LogInformation("wahoo we handling");
        return Task.CompletedTask;
    }
}
