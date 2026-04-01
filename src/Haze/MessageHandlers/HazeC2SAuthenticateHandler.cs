using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SAuthenticateHandler : HazeC2SMessageHandler<HazeC2SAuthenticateMessage>
{
    public HazeC2SAuthenticateHandler(HazeDbContext dbContext) : base(dbContext) { }

    public override async Task Handle(HazeWebSocket webSocket, HazeC2SAuthenticateMessage message, CancellationToken ct = default)
    {
        if (webSocket.ClientSession is null) {
            webSocket.Logger.LogInformation("session not initialised");
            return;
        }
        var client = await DbContext.HazeClients.FindAsync([message.ClientId], ct);
        if (client == null) {
            webSocket.Logger.LogInformation("client does not exist");
            return;
        }
        webSocket.ClientSession.ClientId = client.ClientId;
        await DbContext.SaveChangesAsync(ct);
    }
}
