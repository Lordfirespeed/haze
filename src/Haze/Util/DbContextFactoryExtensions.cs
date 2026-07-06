using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Haze.Util;

public static class DbContextFactoryExtensions
{
    extension<TDbContext>(IDbContextFactory<TDbContext> factory) where TDbContext : DbContext
    {
        public async Task ExecuteRetryingAsync(Func<TDbContext, CancellationToken, Task> action, Func<Exception, bool> shouldRetryOn, int maxRetryCount, CancellationToken ct = default)
        {
            int retryCount = 0;
            while (retryCount <= maxRetryCount) {
                await using var dbContext = await factory.CreateDbContextAsync(ct);
                try {
                    await action(dbContext, ct);
                    break;
                }
                catch (Exception e) when (shouldRetryOn(e)) { }
                retryCount++;
            }
            throw new RetryLimitExceededException();
        }
    }
}
