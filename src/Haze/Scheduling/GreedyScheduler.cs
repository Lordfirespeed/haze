using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using HazeCommon.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Haze.Scheduling;

public class GreedySchedulingService(
    ILogger<GreedySchedulingService> logger,
    IDbContextFactory<HazeDbContext> dbContextFactory,
    HazeConnectionManager connectionManager
) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (true) {
            await Task.Delay(1000, ct);
            await AttemptScheduling(ct);
        }
    }

    protected async Task AttemptScheduling(CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var pendingJobs = dbContext.HazeClientJobs
            .Where(job => job.State == HazeClientJobState.Pending)
            .OrderBy(job => job.CreatedAt)
            .AsAsyncEnumerable();

        await foreach (var pendingJob in pendingJobs.WithCancellation(ct)) {
            await using var jobDbContext = await dbContextFactory.CreateDbContextAsync(ct);
            jobDbContext.Attach(pendingJob);
            // todo: there should probably be a try/catch here
            await AttemptToStartJob(pendingJob, jobDbContext, ct);
            await jobDbContext.SaveChangesAsync(ct);
        }
    }

    protected Task<SteamAccountCredential?> ResolveAvailableCredential(HazeClientJob job, HazeDbContext dbContext, CancellationToken ct)
    {
        return dbContext.SteamAccountCredentials
            .Where(credential => credential.Usage == SteamAccountCredentialUsage.ForLease)
            .Where(credential => credential.Allocations.All(
                allocation => allocation.Job.State != HazeClientJobState.Running
            ))
            .Where(credential => job.RequestedDepots.All(
                depot => depot.Packages.Any(
                    package => package.Licenses.Any(
                        license => license.OwnerAccountId == credential.SteamAccountId
                    )
                )
            ))
            .OrderBy(credential => credential.Account.OwnedLicenses.Count)
            .FirstOrDefaultAsync(ct);
    }

    protected async Task AttemptToStartJob(HazeClientJob job, HazeDbContext dbContext, CancellationToken ct)
    {
        Debug.Assert(job.State is HazeClientJobState.Pending);

        if (!connectionManager.IsConnected(job.OwnerSessionId)) {
            job.PendingReasonCode = "session-disconnected";
            return;
        }

        var credential = await ResolveAvailableCredential(job, dbContext, ct);
        if (credential is null) {
            job.PendingReasonCode = "resources";
            return;
        }

        if (!connectionManager.IsConnected(job.OwnerSessionId)) {
            job.PendingReasonCode = "session-disconnected";
            return;
        }

        dbContext.HazeCredentialAllocations.Add(new HazeCredentialAllocation {
            CredentialId = credential.CredentialId,
            JobId = job.JobId,
        });
        job.State = HazeClientJobState.Running;
        job.PendingReasonCode = null;
        await dbContext.SaveChangesAsync(ct);

        // todo: refresh the credential

        // todo: transmit the credential to the client
        logger.LogInformation("heck yeah it's time to send the credential {}", credential.CredentialId);
    }
}
