using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SAuthenticateHandler : HazeC2SMessageHandler<HazeC2SAuthenticateMessage>
{
    public HazeC2SAuthenticateHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public override async Task Handle(HazeC2SAuthenticateMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        if (context.Session is null) {
            context.Logger.LogInformation("session not initialised");
            return;
        }
        var client = await DbContext.HazeClients.FindAsync([message.ClientId], ct);
        if (client == null) {
            context.Logger.LogInformation("client does not exist");
            return;
        }
        context.Session.ClientId = client.ClientId;
        await DbContext.SaveChangesAsync(ct);
    }
}
