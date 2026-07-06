using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SteamKit2;

namespace Haze.Steam;

public class SteamSyncService(
    ILogger<SteamSyncService> logger,
    IDbContextFactory<HazeDbContext> dbContextFactory
) : BackgroundService {
    protected ConcurrentDictionary<SteamID, SteamSyncConnection> Connections { get; init; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {

    }
}
