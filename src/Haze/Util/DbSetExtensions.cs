using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Steam;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Util;

public static class DbSetExtensions
{
    extension(DbSet<SteamAccount> dbSet)
    {
        public async Task<SteamAccount> FindOrCreateAsync(SteamID steamId, CancellationToken ct = default)
        {
            var dbAccount = await dbSet.FindAsync([steamId], ct);
            if (dbAccount is not null) return dbAccount;
            dbAccount = new SteamAccount { SteamAccountId = steamId };
            dbSet.Add(dbAccount);
            return dbAccount;
        }
    }

    extension(DbSet<SteamPackage> dbSet)
    {
        public async Task<SteamPackage> FindOrCreateAsync(uint packageId, CancellationToken ct = default)
        {
            var dbPackage = await dbSet.FindAsync([packageId], ct);
            if (dbPackage is not null) return dbPackage;
            dbPackage = new SteamPackage { SteamPackageId = packageId, LastChangeNumber = 0 };
            var entry = dbSet.Add(dbPackage);
            // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
            entry.Property(nameof(SteamPackage.SteamPackageId)).CurrentValue = packageId;
            return dbPackage;
        }
    }

    extension(DbSet<SteamLicense> dbSet)
    {
        public async Task<SteamLicense> FindOrCreateAsync(SteamApps.LicenseListCallback.License license, SteamID entitleeId, CancellationToken ct = default)
        {
            var ownerFullId = license.GetOwnerFullId(entitleeId);
            var dbLicense = await dbSet.FindAsync([ownerFullId, license.PackageID], ct);
            if (dbLicense is not null) return dbLicense;
            dbLicense = new SteamLicense
            {
                OwnerAccountId = ownerFullId, PackageId = license.PackageID,
            };
            var entry = dbSet.Add(dbLicense);
            // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
            entry.Property(nameof(SteamLicense.PackageId)).CurrentValue = license.PackageID;
            return dbLicense;
        }
    }

    extension(DbSet<SteamLicenseEntitlement> dbSet)
    {
        public async Task<SteamLicenseEntitlement> FindOrCreateAsync(
            SteamApps.LicenseListCallback.License license,
            SteamID entitleeId,
            CancellationToken ct = default
        ) {
            var ownerFullId = license.GetOwnerFullId(entitleeId);
            var dbEntitlement = await dbSet.FindAsync([entitleeId, ownerFullId, license.PackageID], ct);
            if (dbEntitlement is not null) return dbEntitlement;
            dbEntitlement = new SteamLicenseEntitlement
            {
                EntitledAccountId = entitleeId,
                LicenseOwnerAccountId = ownerFullId,
                LicensePackageId = license.PackageID,
            };
            var entry = dbSet.Add(dbEntitlement);
            // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
            entry.Property(nameof(SteamLicenseEntitlement.LicensePackageId)).CurrentValue = license.PackageID;
            return dbEntitlement;
        }
    }

    extension(DbSet<SteamDepot> dbSet)
    {
        public async Task<SteamDepot> FindOrCreateAsync(uint depotId, CancellationToken ct = default)
        {
            var dbDepot = await dbSet.FindAsync([depotId], ct);
            if (dbDepot is not null) return dbDepot;
            dbDepot = new SteamDepot { SteamDepotId = depotId };
            dbSet.Add(dbDepot);
            return dbDepot;
        }
    }
}
