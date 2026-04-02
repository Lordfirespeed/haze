using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SWhoAmIHandler : HazeC2SMessageHandler<HazeC2SWhoAmIMessage>
{
    public HazeC2SWhoAmIHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    public override async Task Handle(HazeC2SWhoAmIMessage message, HazeMessageHandlerContext context, CancellationToken ct = default)
    {
        await context.QueueS2CMessage(new HazeS2CWhoAmIResponse
        {
            RegardingMessageId = message.MessageId,
            ClientId = context.Session?.ClientId,
        }, ct);
    }
}
