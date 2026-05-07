using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Steam;
using Haze.Util;
using HazeCommon.Messages;
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
            .Where(credential => credential.Account.Credentials
                .Where(otherCredential => otherCredential.Usage == SteamAccountCredentialUsage.ForLease)
                .Count(otherCredential => otherCredential.Allocations.Any(
                    allocation => allocation.Job.State == HazeClientJobState.Running
                )
            ) < 4)
            .OrderBy(credential => credential.Account.OwnedLicenses.Count)
            .FirstOrDefaultAsync(ct);
    }

    protected async Task RefreshCredentialIfNecessary(
        SteamAccountCredential credential,
        HazeDbContext dbContext,
        CancellationToken ct)
    {
        var lastSuccessfulRefresh = await dbContext.SteamAccountCredentialRefreshAttempts
            .OrderByDescending(attempt => attempt.AttemptedAt)
            .FirstOrDefaultAsync(attempt => attempt.CredentialId == credential.CredentialId && attempt.AccessTokenRefreshed, cancellationToken: ct);
        if (lastSuccessfulRefresh is not null && DateTime.UtcNow - lastSuccessfulRefresh.AttemptedAt <= new TimeSpan(0, 5, 0)) return;

        {
            await using var connection = new SteamConnection(logger);
            await connection.Connect();

            connection.DbAuth(credential);
            await connection.LogOn(); // this can raise exceptions
            await connection.RefreshTokenSet();  // in theory, same here

            Debug.Assert(connection.HasAuthenticated);
            credential.SteamAccessToken = connection.TokenSet.AccessToken;
            credential.SteamRefreshToken = connection.TokenSet.RefreshToken;
        }

        await dbContext.SaveChangesAsync(ct);
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
        await RefreshCredentialIfNecessary(credential, dbContext, ct);

        var conn = connectionManager.GetConnection(job.OwnerSessionId);
        if (conn is null) {
            /* if the connection has been dropped, the client will have to get hold of the credential when
            they reconnect. Perhaps the 'resume session' handler should send (a) message(s) to re-establish state. */
            return;
        }
        await conn.QueueS2CMessage(new HazeS2CCredentialReadyMessage
        {
            AccountName = credential.Account.SteamAccountName,
            AccessToken = credential.SteamAccessToken,
            RefreshToken = credential.SteamRefreshToken,
        }, ct);
        logger.LogInformation("heck yeah it's time to send the credential {}", credential.CredentialId);
    }
}
