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
        if (context.Session is not null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_SESSION_EXISTS",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Session already initialised",
            }, ct);
            return;
        }

        var session = await DbContext.HazeClientSessions.FindAsync([message.SessionId], ct);
        if (session is null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_SESSION_NOT_FOUND",
                ErrorTitle = "Not Found",
                ErrorDetail = "Requested session does not exist",
            }, ct);
            return;
        }
        context.Session = session;
    }
}
