using Haze.Models;
using Microsoft.EntityFrameworkCore;

namespace Haze;

// for ref: https://github.com/Lordfirespeed/thunderstore-cli/tree/package-management/StreamBigJson
public class HazeDbContext : DbContext
{
    public DbSet<HazeClient> HazeClients { get; init; }
    public DbSet<HazeClientSession> HazeClientSessions { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseNpgsql("");  // what to put here?
}
