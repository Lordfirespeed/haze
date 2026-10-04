using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task FindOrCreateManyAsync(ICollection<SteamID> accountIds, CancellationToken ct = default)
        {
            var existingAccounts = dbSet.Where(account => accountIds.Contains(account.SteamAccountId)).AsAsyncEnumerable();
            var toCreateIds = accountIds.ToHashSet();
            await foreach (var account in existingAccounts) {
                toCreateIds.Remove(account.SteamAccountId);
            }
            foreach (var accountId in toCreateIds) {
                var dbAccount = new SteamAccount { SteamAccountId = accountId };
                dbSet.Add(dbAccount);
            }
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

        public async Task FindOrCreateManyAsync(ICollection<uint> packageIds, CancellationToken ct = default)
        {
            var existingPackages = dbSet.Where(package => packageIds.Contains(package.SteamPackageId)).AsAsyncEnumerable();
            var toCreateIds = packageIds.ToHashSet();
            await foreach (var package in existingPackages) {
                toCreateIds.Remove(package.SteamPackageId);
            }
            foreach (var packageId in toCreateIds) {
                var dbPackage = new SteamPackage { SteamPackageId = packageId, LastChangeNumber = 0 };
                var entry = dbSet.Add(dbPackage);
                // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
                entry.Property(nameof(SteamPackage.SteamPackageId)).CurrentValue = packageId;
            }
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

        public IQueryable<SteamLicense> WhereKeyIn(ICollection<LicensePrimaryKey> keys)
        {
            var keyTuplesString = String.Join(",", keys.Select(key => key.ToQueryString()));
#pragma warning disable EF1002
            return dbSet.FromSqlRaw(
                $"""
                SELECT * FROM "SteamLicenses" WHERE ("OwnerAccountId", "PackageId") in ({keyTuplesString})
                """
            );
#pragma warning restore EF1002
        }

        public async Task FindOrCreateManyAsync(
            IEnumerable<SteamApps.LicenseListCallback.License> licenses,
            SteamID entitleeId,
            DateTime seenTime,
            CancellationToken ct = default
        ) {
            var toCreateLicenseKeys = licenses.Select(
                license => LicensePrimaryKey.FromLicenseList(license, entitleeId)
            ).ToHashSet();
            var existingLicenses = dbSet.WhereKeyIn(toCreateLicenseKeys).AsAsyncEnumerable();
            await foreach (var license in existingLicenses) {
                toCreateLicenseKeys.Remove(LicensePrimaryKey.FromDb(license));
                license.LastSeen = seenTime;
            }
            foreach (var licenseKey in toCreateLicenseKeys) {
                var dbLicense = new SteamLicense {
                    OwnerAccountId = licenseKey.OwnerId,
                    PackageId = licenseKey.PackageId,
                    LastSeen = seenTime,
                };
                var entry = dbSet.Add(dbLicense);
                // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
                entry.Property(nameof(SteamLicense.PackageId)).CurrentValue = licenseKey.PackageId;
            }
        }
    }

    public sealed record LicensePrimaryKey(SteamID OwnerId, uint PackageId)
    {
        public bool Matches(SteamLicense dbLicense) =>
            dbLicense.OwnerAccountId == OwnerId && dbLicense.PackageId == PackageId;

        public static LicensePrimaryKey FromLicenseList(SteamApps.LicenseListCallback.License license, SteamID entitleeId)
        {
            return new LicensePrimaryKey(license.GetOwnerFullId(entitleeId), license.PackageID);
        }

        public static LicensePrimaryKey FromDb(SteamLicense dbLicense)
        {
            return new LicensePrimaryKey(dbLicense.OwnerAccountId, dbLicense.PackageId);
        }

        public string ToQueryString() => $"({OwnerId.ConvertToUInt64()},{PackageId})";
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

        public async Task FindOrCreateManyAsync(ICollection<uint> depotIds, CancellationToken ct = default)
        {
            var existingDbApps = dbSet.Where(depot => depotIds.Contains(depot.SteamDepotId)).AsAsyncEnumerable();
            var toCreateIds = depotIds.ToHashSet();
            await foreach (var depot in existingDbApps) {
                toCreateIds.Remove(depot.SteamDepotId);
            }
            foreach (var depotId in toCreateIds) {
                var dbDepot = new SteamDepot { SteamDepotId = depotId };
                dbSet.Add(dbDepot);
            }
        }
    }

    extension(DbSet<SteamApp> dbSet)
    {
        public async Task<SteamApp> FindOrCreateAsync(uint appId, CancellationToken ct = default)
        {
            var dbApp = await dbSet.FindAsync([appId], ct);
            if (dbApp is not null) return dbApp;
            dbApp = new SteamApp { SteamAppId = appId, LastChangeNumber = 0 };
            dbSet.Add(dbApp);
            return dbApp;
        }

        public async Task FindOrCreateManyAsync(ICollection<uint> appIds, CancellationToken ct = default)
        {
            var existingDbApps = dbSet.Where(app => appIds.Contains(app.SteamAppId)).AsAsyncEnumerable();
            var toCreateIds = appIds.ToHashSet();
            await foreach (var app in existingDbApps) {
                toCreateIds.Remove(app.SteamAppId);
            }
            foreach (var appId in toCreateIds) {
                var dbApp = new SteamApp { SteamAppId = appId, LastChangeNumber = 0 };
                dbSet.Add(dbApp);
            }
        }
    }
}
