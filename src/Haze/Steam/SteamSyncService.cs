using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HazeCommon.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SteamKit2;

namespace Haze.Steam;

public class SteamSyncService(
    ILogger<SteamSyncService> logger,
    IDbContextFactory<HazeDbContext> dbContextFactory
) : IHostedService {
    protected ConcurrentDictionary<SteamID, SteamSyncConnection> Connections { get; init; }

    private static readonly BoundedChannelOptions WorkQueueOptions = new(3)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    };

    public async Task StartAsync(CancellationToken ct)
    {
        // this only 'discovers' new accounts to listen on when initially started.
        // a credential table listener should be added to remedy this
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var suitableAccounts = dbContext.SteamAccounts
            .Where(account => account.SteamAccountName != null)
            .Where(account => account.Credentials.Any(cred => cred.Usage == SteamAccountCredentialUsage.HazeOnly))
            .AsAsyncEnumerable();

        await foreach (var account in suitableAccounts.WithCancellation(ct)) {
            var workQueue = Channel.CreateBounded<Func<Task>>(WorkQueueOptions);
            var syncConnection = new SteamSyncConnection(logger, dbContextFactory, account, workQueue);
            await syncConnection.Start(ct);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        foreach (var (steamId, syncConnection) in Connections) {
            await syncConnection.DisposeAsync();
        }
        Connections.Clear();
    }
}
