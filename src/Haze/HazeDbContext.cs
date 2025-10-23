using Haze.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SteamKit2;

namespace Haze;

// for ref: https://github.com/Lordfirespeed/thunderstore-cli/tree/package-management/StreamBigJson
public class HazeDbContext : DbContext
{
    public DbSet<HazeClient> HazeClients { get; init; }
    public DbSet<HazeClientSession> HazeClientSessions { get; init; }

    public DbSet<SteamAccount> SteamAccounts { get; init; }
    public DbSet<SteamAccountCredential> SteamAccountCredentials { get; init; }
    public DbSet<SteamLicense> SteamLicenses { get; init; }
    public DbSet<SteamPackage> SteamPackages { get; init; }
    public DbSet<SteamLicenseEntitlement> SteamLicenseEntitlements { get; init; }
    public DbSet<SteamApp> SteamApps { get; init; }
    public DbSet<SteamDepot> SteamDepots { get; init; }
    public DbSet<SteamAppDepotConfig> SteamAppDepotConfigs { get; init; }

    private static readonly ValueConverter<SteamID, ulong> SteamIdValueConverter = new(
        v => v.ConvertToUInt64(),
        v => new SteamID(v)
    );

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseNpgsql("Host=localhost; Username=haze; Password=haze; Database=haze;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SteamAccount>()
            .HasMany(account => account.Licenses)
            .WithMany(license => license.EntitledAccounts)
            .UsingEntity<SteamLicenseEntitlement>();

        modelBuilder
            .Entity<SteamAccount>()
            .Property(account => account.SteamAccountId)
            .HasConversion(SteamIdValueConverter);

        modelBuilder
            .Entity<SteamAccountCredential>()
            .Property(credential => credential.SteamAccountId)
            .HasConversion(SteamIdValueConverter);

        modelBuilder
            .Entity<SteamLicense>()
            .Property(license => license.OwnerAccountId)
            .HasConversion(SteamIdValueConverter);

        modelBuilder
            .Entity<SteamLicenseEntitlement>()
            .Property(license => license.EntitledAccountId)
            .HasConversion(SteamIdValueConverter);
        modelBuilder
            .Entity<SteamLicenseEntitlement>()
            .Property(license => license.LicenseOwnerAccountId)
            .HasConversion(SteamIdValueConverter);
    }
}
