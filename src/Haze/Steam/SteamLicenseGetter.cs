using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Haze.Models;
using Haze.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.GC.TF2.Internal;

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
            .FirstOrDefaultAsync(cred => cred.CredentialId == 2, cancellationToken: ct);
        if (cred is null) throw new InvalidOperationException();
        _account = cred.Account;

        await using var connection = new SteamConnection(logger);
        await connection.Connect();

        connection.DbAuth(cred);

        using var onLicenseListCallback = connection.Manager.Subscribe<SteamApps.LicenseListCallback>(
            async Task (callback) => {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
                try {
                    await OnLicenseList(callback, dbContext, ct);
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
        }

        {
            uint interestingAppId = 1966720;
            var accessTokensResult = await connection.Apps.PICSGetAccessTokens([interestingAppId], []);
            var appRequest = new SteamApps.PICSRequest(interestingAppId, accessTokensResult.AppTokens[interestingAppId]);
            var resultSet = await connection.Apps.PICSGetProductInfo([appRequest], []);
            await DumpPICSProductInfo(resultSet, ct);
        }
    }

    public async Task OnLicenseList(SteamApps.LicenseListCallback callback, HazeDbContext dbContext, CancellationToken ct = default)
    {
        logger.LogInformation($"Got a license list at {DateTime.Now} (local time)");
        if (callback.Result is not EResult.OK) throw new Exception();

        var entitleeFullId = _account.SteamAccountId;
        var seenTime = DateTime.UtcNow;

        var packageTokens = new Dictionary<uint, ulong>();
        foreach (var license in callback.LicenseList) {
            if (license.AccessToken > 0) packageTokens[license.PackageID] = license.AccessToken;
        }

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

        _lastLicenseList = callback.LicenseList;
        _packageTokens = packageTokens.ToImmutableDictionary();
        if (!_firstLicenseListTcs.Task.IsCompleted) _firstLicenseListTcs.SetResult();
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
}
