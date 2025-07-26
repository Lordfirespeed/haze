using Microsoft.EntityFrameworkCore;

namespace Haze;

// for ref: https://github.com/Lordfirespeed/thunderstore-cli/tree/package-management/StreamBigJson
public class HazeDbContext : DbContext
{
    public DbSet<string> Strings { get; set; } // is 'init' allowed here?

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseNpgsql("");  // what to put here?
}
