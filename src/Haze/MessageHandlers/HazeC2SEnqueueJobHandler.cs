using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Messages;
using Microsoft.Extensions.Logging;

namespace Haze.MessageHandlers;

public class HazeC2SEnqueueJobHandler : HazeC2SMessageHandler<HazeC2SEnqueueJobMessage>
{
    public HazeC2SEnqueueJobHandler(HazeDbContext dbContext, ILogger logger) : base(dbContext, logger) { }

    async Task<(uint depotId, SteamDepot? depot)> LookupDepot(uint depotId, CancellationToken ct = default)
    {
        return (depotId, await DbContext.SteamDepots.FindAsync([depotId], ct));
    }

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

        if (message.DepotIds.Length == 0)
        {
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_BAD_JOB_NO_DEPOTS",
                ErrorTitle = "Bad Message",
                ErrorDetail = "Job must include at least one depot ID",
            }, ct);
            return;
        }

        // could be replaced with a Task.WhenEach w/ cancellation of remaining tasks
        var depotLookupTasks = message.DepotIds.Select(id => LookupDepot(id, ct)).ToImmutableArray();
        var depotLookupResults = await Task.WhenAll(depotLookupTasks);
        foreach (var result in depotLookupResults)
        {
            if (result.depot is not null) continue;
            await context.QueueS2CMessage(new HazeS2CErrorMessage
            {
                RegardingMessageId = message.MessageId,
                ErrorCode = "ERR_DEPOT_NOT_FOUND",
                ErrorTitle = "Not Found",
                ErrorDetail = $"Depot {result.depotId} is not known to this Haze instance"
            }, ct);
            return;
        }

        // enqueue the job!
        context.Session.Job = new HazeClientJob
        {
            OwnerSessionId = context.Session.SessionId,
            CreatedAt = DateTime.UtcNow,
            State = HazeClientJobState.Pending,
            PendingReasonCode = "none",
        };
        foreach (var result in depotLookupResults)
        {
            context.Session.Job.RequestedDepots.Add(result.depot!);
        }
        await DbContext.SaveChangesAsync(ct);
        await context.QueueS2CMessage(new HazeS2CSuccessMessage() {
            RegardingMessageId = message.MessageId,
        }, ct);
    }
}
