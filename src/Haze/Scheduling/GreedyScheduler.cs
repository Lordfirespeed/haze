using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Haze.Scheduling;

public class GreedySchedulingService(ILogger<GreedySchedulingService> logger, IDbContextFactory<HazeDbContext> dbContextFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("wahoo {}", dbContextFactory);
        return Task.CompletedTask;
    }
}
