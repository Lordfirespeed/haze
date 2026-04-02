using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SResumeSessionHandler : HazeC2SMessageHandler<HazeC2SResumeSessionMessage>
{
    public HazeC2SResumeSessionHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public override async Task Handle(HazeC2SResumeSessionMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        var session = await DbContext.HazeClientSessions.FindAsync([message.SessionId], ct);
        if (session is null) {
            context.Logger.LogInformation("Session not found");
            return;
        }
        context.Session = session;
    }
}
