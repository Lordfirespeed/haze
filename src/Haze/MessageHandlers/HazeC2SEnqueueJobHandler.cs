using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SEnqueueJobHandler : HazeC2SMessageHandler<HazeC2SEnqueueJobMessage>
{
    public HazeC2SEnqueueJobHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public override async Task Handle(HazeC2SEnqueueJobMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        if (context.Session is null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_NO_SESSION",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Session not initialised",
            }, ct);
            return;
        }

        if (context.Session.Job is not null) {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_JOB_EXISTS",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Session already has an associated job",
            }, ct);
            return;
        }

        // enqueue the job!
    }
}
