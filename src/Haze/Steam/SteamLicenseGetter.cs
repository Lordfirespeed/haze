using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteamKit2;

namespace Haze.Steam;

public class SteamLicenseGetter(IDbContextFactory<HazeDbContext> dbContextFactory, ILogger<SteamLicenseGetter> logger)
{
    private SteamAccount _account = null!;

    private ReadOnlyCollection<SteamApps.LicenseListCallback.License> _lastLicenseList = [];
    private ImmutableDictionary<uint, ulong> _packageTokens = [];
    private readonly TaskCompletionSource _firstLicenseListTcs = new();

    private ReadOnlyCollection<SteamApps.PICSProductInfoCallback.PICSProductInfo> _packageInfoList = [];

    private ImmutableDictionary<uint, ulong> _appTokens = [];
    private ImmutableDictionary<uint, byte[]> _depotKeys = [];


    private async Task DumpPICSProductInfo(AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet resultSet, CancellationToken ct = default)
    {
        Debug.Assert(resultSet.Results is not null);
        foreach (var (appId, appInfo) in resultSet.Results.SelectMany(result => result.Apps))
        {
            logger.LogInformation($"app {appId}, {await Checksum.Sha256SumObject(appInfo, ct)}");
            logger.LogDebug(JsonSerializer.Serialize(appInfo));
        }

        foreach (var (packageId, packageInfo) in resultSet.Results.SelectMany(result => result.Packages))
        {
            logger.LogInformation($"package {packageId}, {await Checksum.Sha256SumObject(packageInfo, ct)}");
            logger.LogDebug(JsonSerializer.Serialize(packageInfo));
            ExtractPackageRelations(packageInfo);
        }
    }

    private void ExtractPackageRelations(SteamApps.PICSProductInfoCallback.PICSProductInfo info)
    {
        var appIds = info.KeyValues["appids"].Children
            .Select(child => child.AsUnsignedInteger())
            .ToImmutableArray();
        var depotIds = info.KeyValues["depotids"].Children
            .Select(child => child.AsUnsignedInteger())
            .ToImmutableArray();
        logger.LogInformation("app IDs [{appIds}], depot IDs [{depotIds}]", appIds, depotIds);
    }

    public async Task Foo(CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var cred = await dbContext.SteamAccountCredentials
            .Include(cred => cred.Account)
            .FirstOrDefaultAsync(cred => cred.CredentialId == 1, cancellationToken: ct);
        if (cred is null) throw new InvalidOperationException();
        _account = cred.Account;

        await using var connection = new SteamConnection(logger);
        await connection.Connect();

        connection.DbAuth(cred);

        using var dbLicenseStuff = connection.Manager.Subscribe<SteamApps.LicenseListCallback>(
            async Task (callback) => {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
                try {
                    await NonDatabaseLicenseListStuff(callback);
                    await DatabaseLicenseListStuff(callback, dbContext, ct);
                    await ProductInfoStuff(connection, dbContext, ct);
                } catch (Exception exc) {
                    logger.LogError(exc, "Exception thrown in license list callback handler");
                }
            }
        );
        await connection.LogOn();

        await _firstLicenseListTcs.Task;


        // ref: https://github.com/SteamRE/SteamKit/issues/531#issuecomment-377963355
        {
            var packageRequests = _lastLicenseList
                .Select(license => license.PackageID)
                .Select(id => new SteamApps.PICSRequest(id, _packageTokens.TryGetValue(id, out var token) ? token : 0));
            var resultSet = await connection.Apps.PICSGetProductInfo(apps: [], packages: packageRequests);
            _packageInfoList = resultSet.Results!.SelectMany(result => result.Packages.Values)
                .ToImmutableArray()
                .AsReadOnly();
            await DumpPICSProductInfo(resultSet, ct);
        }

        {
            uint interestingAppId = 1966720;
            var accessTokensResult = await connection.Apps.PICSGetAccessTokens([interestingAppId], []);
            if (accessTokensResult.AppTokensDenied.Contains(interestingAppId)) {
                throw new Exception("oof you got rejected mate");
            }
            var appRequest = new SteamApps.PICSRequest(interestingAppId, accessTokensResult.AppTokens[interestingAppId]);
            var resultSet = await connection.Apps.PICSGetProductInfo([appRequest], []);
            await DumpPICSProductInfo(resultSet, ct);
        }


        await Task.Delay(Timeout.Infinite, ct);
    }

    public async Task NonDatabaseLicenseListStuff(SteamApps.LicenseListCallback callback)
    {
        logger.LogInformation($"Got a license list at {DateTime.Now} (local time)");
        if (callback.Result is not EResult.OK) throw new Exception();

        var packageTokens = new Dictionary<uint, ulong>();
        foreach (var license in callback.LicenseList) {
            if (license.AccessToken > 0) packageTokens[license.PackageID] = license.AccessToken;
        }

        await using var handle = File.OpenWrite($"license-list-{DateTime.Now:o}");
        await JsonSerializer.SerializeAsync(handle, callback);

        _lastLicenseList = callback.LicenseList;
        _packageTokens = packageTokens.ToImmutableDictionary();
        if (!_firstLicenseListTcs.Task.IsCompleted) _firstLicenseListTcs.SetResult();
    }

    public async Task DatabaseLicenseListStuff(SteamApps.LicenseListCallback callback, HazeDbContext dbContext, CancellationToken ct = default)
    {
        if (callback.Result is not EResult.OK) throw new Exception();

        var entitleeFullId = _account.SteamAccountId;
        var seenTime = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var uniqueOwners = callback.LicenseList
            .Select(license => license.GetOwnerFullId(entitleeFullId))
            .ToImmutableHashSet();
        foreach (var owner in uniqueOwners) {
            await UpsertAccount(owner);
        }
        foreach (var license in callback.LicenseList) {
            await UpsertLicensePackage(license);
        }
        await dbContext.SaveChangesAsync(ct);

        foreach (var license in callback.LicenseList) {
            await UpsertLicense(license);
        }
        await dbContext.SaveChangesAsync(ct);

        foreach (var license in callback.LicenseList) {
            await UpsertEntitlement(license);
        }
        await dbContext.SaveChangesAsync(ct);

        await dbContext.SteamLicenses
            .Where(license => license.Entitlements
                .All(entitlement => entitlement.EntitledAccountId == entitleeFullId && entitlement.LastSeen < seenTime)
            )
            .ExecuteDeleteAsync(ct);
        await dbContext.SteamLicenseEntitlements
            .Where(entitlement => entitlement.EntitledAccountId == entitleeFullId)
            .Where(entitlement => entitlement.LastSeen < seenTime)
            .ExecuteDeleteAsync(ct);
        await dbContext.SteamLicenses
            .Where(license => license.OwnerAccountId == entitleeFullId)
            .Where(license => license.LastSeen < seenTime)
            .ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Done upserting accounts, packages, licenses, and entitlements");
        return;

        async Task UpsertAccount(SteamID accountId)
        {
            if (accountId == entitleeFullId) return; // assume entitlee is already in the database
            var dbOwner = await dbContext.SteamAccounts.FindAsync([accountId], ct);
            if (dbOwner is not null) return;
            dbOwner = new SteamAccount { SteamAccountId = accountId };
            dbContext.SteamAccounts.Add(dbOwner);
        }

        async Task UpsertLicensePackage(SteamApps.LicenseListCallback.License license)
        {
            var dbPackage = await dbContext.SteamPackages.FindAsync([license.PackageID], ct);
            if (dbPackage is not null) return;

            dbPackage = new SteamPackage { SteamPackageId = license.PackageID, LastChangeNumber = 0 };
            dbContext.SteamPackages.Add(dbPackage);

        }

        async Task UpsertLicense(SteamApps.LicenseListCallback.License license)
        {
            var ownerFullId = license.GetOwnerFullId(_account.SteamAccountId);

            var dbLicense = await dbContext.SteamLicenses.FindAsync(
                [ownerFullId, license.PackageID], ct
            );
            if (dbLicense is null) {
                dbLicense = new SteamLicense {
                    OwnerAccountId = ownerFullId, PackageId = license.PackageID,
                };
                var entry = dbContext.SteamLicenses.Add(dbLicense);
                // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
                entry.Property(nameof(SteamLicense.PackageId)).CurrentValue = license.PackageID;
            }
            dbLicense.LastSeen = seenTime;
        }

        async Task UpsertEntitlement(SteamApps.LicenseListCallback.License license)
        {
            var ownerFullId = license.GetOwnerFullId(_account.SteamAccountId);

            var dbEntitlement = await dbContext.SteamLicenseEntitlements.FindAsync(
                [entitleeFullId, ownerFullId, license.PackageID], ct
            );
            if (dbEntitlement is null) {
                dbEntitlement = new SteamLicenseEntitlement
                {
                    EntitledAccountId = entitleeFullId,
                    LicenseOwnerAccountId = ownerFullId,
                    LicensePackageId = license.PackageID,
                };
                var entry = dbContext.SteamLicenseEntitlements.Add(dbEntitlement);
                // change tracker thinks package ID is unset if the package ID is zero - force it to accept package ID
                entry.Property(nameof(SteamLicenseEntitlement.LicensePackageId)).CurrentValue = license.PackageID;
            }
            dbEntitlement.LastSeen = seenTime;
        }
    }

    public async Task ProductInfoStuff(SteamConnection connection, HazeDbContext dbContext, CancellationToken ct = default)
    {
        var refreshStartedAt = DateTime.UtcNow;

        var lastRefreshAttempt = await dbContext.SteamAccountProductInfoRefreshAttempts
            .Where(attempt => attempt.SteamAccountId == _account.SteamAccountId)
            .OrderByDescending(attempt => attempt.AttemptCompletedAt)
            .FirstOrDefaultAsync(ct);
        var changes = await connection.Apps.PICSGetChangesSince(
            lastRefreshAttempt?.LastChangeNumber ?? 0,
            sendAppChangelist: true,
            sendPackageChangelist: true
        );
        var toUpdatePackageIds = _lastLicenseList.Select(license => license.PackageID).ToHashSet();
        await ReduceToUpdatePackageIdSet(changes);

        var packageRequests = _lastLicenseList
            .Select(license => license.PackageID)
            .Select(id => new SteamApps.PICSRequest(id, _packageTokens.TryGetValue(id, out var token) ? token : 0));
        var packageResultSet = await connection.Apps.PICSGetProductInfo(apps: [], packages: packageRequests);
        _packageInfoList = packageResultSet.Results!.SelectMany(result => result.Packages.Values)
            .ToImmutableArray()
            .AsReadOnly();

        foreach (var package in _packageInfoList) {
            if (!toUpdatePackageIds.Contains(package.ID)) continue;

            var dbPackage = await dbContext.SteamPackages
                .Include(p => p.Depots)
                .FirstOrDefaultAsync(p => p.SteamPackageId == package.ID, ct);
            Debug.Assert(dbPackage is not null);
            dbPackage.LastChangeNumber = package.ChangeNumber;

            var depotIds = package.KeyValues["depotids"].Children
                .Select(child => child.AsUnsignedInteger())
                .ToImmutableArray();
            foreach (var depotId in depotIds) {
                var dbDepot = dbContext.SteamDepots.Local.FindEntry(depotId)?.Entity;
                if (dbDepot is null) {
                    dbDepot = await dbContext.SteamDepots.FindAsync([depotId], ct);
                }
                if (dbDepot is null) {
                    dbDepot = new SteamDepot { SteamDepotId = depotId };
                    dbContext.SteamDepots.Add(dbDepot);
                }
                dbPackage.Depots.Add(dbDepot);
            }
        }
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Done upserting depots and package <-> depot relations");

        var toRequestAppIds = new HashSet<uint>();
        foreach (var package in _packageInfoList) {
            var appIds = package.KeyValues["appids"].Children.Select(child => child.AsUnsignedInteger());
            toRequestAppIds.UnionWith(appIds);
        }
        await ReduceToRequestAppIdSet(changes);

        var appTokensResult = await connection.Apps.PICSGetAccessTokens(appIds: toRequestAppIds, packageIds: []);
        _appTokens = appTokensResult.AppTokens.ToImmutableDictionary();
        var appRequests = toRequestAppIds
            .Select(id => new SteamApps.PICSRequest(id, _appTokens.TryGetValue(id, out var token) ? token : 0));
        var appResultSet = await connection.Apps.PICSGetProductInfo(apps: appRequests, packages: []);
        var appInfoList = appResultSet.Results!.SelectMany(result => result.Apps.Values)
            .ToImmutableArray()
            .AsReadOnly();
        foreach (var app in appInfoList) {
            await UpsertApp(app);
        }
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Done upserting apps");

        foreach (var package in _packageInfoList) {
            if (!toUpdatePackageIds.Contains(package.ID)) continue;
            var dbPackage = await dbContext.SteamPackages
                .Include(p => p.Apps)
                .FirstOrDefaultAsync(p => p.SteamPackageId == package.ID, ct);
            Debug.Assert(dbPackage is not null);
            var appIds = package.KeyValues["appids"].Children.Select(child => child.AsUnsignedInteger());
            foreach (var appId in appIds) {
                var dbApp = dbContext.SteamApps.Local.FindEntry(appId)?.Entity;
                if (dbApp is null) {
                    dbApp = await dbContext.SteamApps.FindAsync([appId], ct);
                }
                Debug.Assert(dbApp is not null);
                dbPackage.Apps.Add(dbApp);
            }
        }
        logger.LogInformation("Done creating package <-> app relations");
        await dbContext.SaveChangesAsync(ct);

        var attempt = new SteamAccountProductInfoRefreshAttempt {
            SteamAccountId = _account.SteamAccountId,
            AttemptStartedAt = refreshStartedAt,
            AttemptCompletedAt = DateTime.UtcNow,
            LastChangeNumber = changes.CurrentChangeNumber,
        };
        dbContext.SteamAccountProductInfoRefreshAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Done creating record of this product info refresh attempt");
        return;

        async Task UpsertApp(SteamApps.PICSProductInfoCallback.PICSProductInfo appInfo)
        {
            var dbApp = await dbContext.SteamApps.FindAsync([appInfo.ID], ct);
            if (dbApp is null) {
                dbApp = new SteamApp { SteamAppId = appInfo.ID };
                dbContext.Add(dbApp);
            }
            dbApp.LastChangeNumber = appInfo.ChangeNumber;
        }

        async Task ReduceToUpdatePackageIdSet(SteamApps.PICSChangesCallback changes)
        {
            if (changes.RequiresFullUpdate || changes.RequiresFullPackageUpdate) return;

            toUpdatePackageIds.Clear();
            foreach (var (key, change) in changes.PackageChanges) {
                toUpdatePackageIds.Add(change.ID);
            }
        }

        async Task ReduceToRequestAppIdSet(SteamApps.PICSChangesCallback changes)
        {
            if (changes.RequiresFullUpdate || changes.RequiresFullAppUpdate) return;

            var alreadyKnownApps = dbContext.SteamApps
                .Where(app => toRequestAppIds.Contains(app.SteamAppId))
                .AsAsyncEnumerable();
            await foreach (var alreadyKnownApp in alreadyKnownApps.WithCancellation(ct)) {
                toRequestAppIds.Remove(alreadyKnownApp.SteamAppId);
            }
            foreach (var (key, change) in changes.AppChanges) {
                toRequestAppIds.Add(change.ID);
            }
        }
    }
}
