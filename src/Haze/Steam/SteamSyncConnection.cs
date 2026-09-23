using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EntityFrameworkCore.Locking;
using Haze.Models;
using Haze.Util;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SteamKit2;

namespace Haze.Steam;

public record SteamSyncRefreshContext
{
    public required SteamSyncConnection SyncConnection { get; init; }
    public required SteamApps.LicenseListCallback Message { get; init; }

    public DateTime StartedAtUtc { get; set; }

    public SteamConnection Connection => SyncConnection.Connection;
    public IDbContextFactory<HazeDbContext> DbContextFactory => SyncConnection.DbContextFactory;
    public SteamID AccountId => Connection.IsLoggedOn ? Connection.AccountId : throw new InvalidOperationException();
    public IReadOnlyList<SteamApps.LicenseListCallback.License> Licenses => Message.LicenseList;
    public IEnumerable<uint> LicensePackageIds => Licenses.Select(license => license.PackageID);
    public IDictionary<uint, ulong> PackageTokens { get; } = new Dictionary<uint, ulong>();
    public IImmutableList<SteamApps.PICSProductInfoCallback.PICSProductInfo>? PackageInfos { get; set; }
    public ISet<uint> PackageInfosAppIds {
        get {
            if (PackageInfos is null) throw new InvalidOperationException();
            return PackageInfos.SelectMany(
                p => p.KeyValues["appids"].Children.Select(child => child.AsUnsignedInteger())
            ).ToHashSet();
        }
    }

    public IDictionary<uint, ulong> AppTokens { get; } = new Dictionary<uint, ulong>();
    public IImmutableList<SteamApps.PICSProductInfoCallback.PICSProductInfo>? AppInfos { get; set; }

    public void PopulatePackageTokensFromLicenseList()
    {
        foreach (var license in Licenses) {
            if (license.AccessToken > 0) PackageTokens[license.PackageID] = license.AccessToken;
        }
    }

    public async Task PopulateAppTokensByRequesting()
    {
        var tokensResult = await Connection.Apps.PICSGetAccessTokens(appIds: PackageInfosAppIds, packageIds: []);
        foreach (var (appId, appToken) in tokensResult.AppTokens) {
            if (appToken > 0) AppTokens[appId] = appToken;
        }
    }

    public Task<SteamAccountProductInfoRefreshAttempt?> GetLastSyncAttempt(HazeDbContext dbContext, CancellationToken ct = default)
    {
        return dbContext.SteamAccountProductInfoRefreshAttempts
            .Where(attempt => attempt.SteamAccountId == AccountId)
            .OrderByDescending(attempt => attempt.AttemptCompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    private SteamApps.PICSChangesCallback? _changesSinceLastSync;
    public async Task<SteamApps.PICSChangesCallback> GetChangesSinceLastSync(HazeDbContext dbContext, CancellationToken ct = default)
    {
        if (_changesSinceLastSync is not null) return _changesSinceLastSync;
        var lastSyncAttempt = await GetLastSyncAttempt(dbContext, ct);
        var changes = await Connection.Apps.PICSGetChangesSince(
            lastSyncAttempt?.LastChangeNumber ?? 0,
            sendAppChangelist: true,
            sendPackageChangelist: true
        );
        _changesSinceLastSync = changes;
        return changes;
    }

    public async Task<ISet<uint>> GetPackageIdsWithChanges(HazeDbContext dbContext, CancellationToken ct = default)
    {
        var changes = await GetChangesSinceLastSync(dbContext, ct);
        if (changes.RequiresFullUpdate || changes.RequiresFullPackageUpdate) {
            return LicensePackageIds.ToHashSet();
        }
        return changes.PackageChanges.Values.Select(change => change.ID).ToHashSet();
    }

    public async Task<ISet<uint>> GetAppIdsWithChanges(HazeDbContext dbContext, CancellationToken ct = default)
    {
        if (AppInfos is null) throw new InvalidOperationException();
        var changes = await GetChangesSinceLastSync(dbContext, ct);
        if (changes.RequiresFullUpdate || changes.RequiresFullAppUpdate) {
            return AppInfos.Select(app => app.ID).ToHashSet();
        }
        return changes.AppChanges.Values.Select(change => change.ID).ToHashSet();
    }
}

public class SteamSyncConnection
{
    public static TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

    public required IDbContextFactory<HazeDbContext> DbContextFactory { get; init; }
    public required SteamConnection Connection { get; init; }
    public required Channel<Func<Task>> WorkQueue { get; init; }

    public DateTime? LastRefreshTriggeredAt { get; set; }
    public bool ShouldLicenseListTriggerRefresh
        => LastRefreshTriggeredAt == null || (DateTime.UtcNow - LastRefreshTriggeredAt) >= RefreshDelay;

    async Task OnLicenseList(SteamApps.LicenseListCallback message)
    {
        if (message.Result is not EResult.OK) return;
        if (!ShouldLicenseListTriggerRefresh) return;
        LastRefreshTriggeredAt = DateTime.UtcNow;
        await Task.Delay(RefreshDelay);
        var ctx = new SteamSyncRefreshContext
        {
            SyncConnection = this,
            Message = message,
        };
        await WorkQueue.Writer.WriteAsync(Task () => RefreshEverything(ctx));
    }

    async Task RefreshEverything(SteamSyncRefreshContext context)
    {
        await RefreshLicenses(context);
        await RefreshDepots(context);
        await RefreshApps(context);
        await RefreshPackageRelations(context);
    }

    async Task RefreshLicenses(SteamSyncRefreshContext context, CancellationToken ct = default)
    {
        var seenTime = DateTime.UtcNow;
        var uniqueOwnerIds = context.Licenses
            .Select(license => license.GetOwnerFullId(context.AccountId))
            .ToImmutableHashSet();

        // inserts of accounts and packages use optimistic concurrency and should retry on unique constraint violations
        await context.DbContextFactory.ExecuteRetryingAsync(async (dbContext, ct) =>
            {
                foreach (var ownerId in uniqueOwnerIds) {
                    if (ownerId == context.AccountId) continue;
                    await EnsureAccount(dbContext, ownerId);
                }
                foreach (var license in context.Licenses) {
                    await EnsurePackage(dbContext, license.PackageID);
                }
                await dbContext.SaveChangesAsync(ct);
            },
            (exception) => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation },
            6,
            ct
        );

        // inserts of licenses owned by the entitlee have no concurrent concerns
        {
            await using var dbContext = await context.DbContextFactory.CreateDbContextAsync(ct);
            foreach (var license in context.Licenses) {
                if (!license.IsOwnedBy(context.AccountId)) continue;
                await UpsertLicense(dbContext, license, ct);
            }
            await dbContext.SaveChangesAsync(ct);
        }

        // inserts of licenses *not* owned by the entitlee use optimistic concurrency and should retry on unique constraint violations
        await context.DbContextFactory.ExecuteRetryingAsync(async (dbContext, ct) =>
            {
                foreach (var license in context.Licenses) {
                    if (license.IsOwnedBy(context.AccountId)) continue;
                    await UpsertLicense(dbContext, license, ct);
                }
                await dbContext.SaveChangesAsync(ct);
            },
            (exception) => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation },
            6,
            ct
        );

        // inserts of entitlements have no concurrent concerns
        {
            await using var dbContext = await context.DbContextFactory.CreateDbContextAsync(ct);
            foreach (var license in context.Licenses) {
                await UpsertEntitlement(dbContext, license, ct);
            }
            await dbContext.SaveChangesAsync(ct);
        }

        {
            await using var dbContext = await context.DbContextFactory.CreateDbContextAsync(ct);

            // deletes of steam licenses with no entitlements remaining have concurrent concerns
            // but the desired result should be achieved without retries
            // note also: this operation also deletes some entitlements via cascade
            await dbContext.SteamLicenses
                .Where(license => license.LastSeen < seenTime)
                .Where(license => license.Entitlements
                    .All(entitlement => entitlement.EntitledAccountId == context.AccountId && entitlement.LastSeen < seenTime)
                )
                .ExecuteDeleteAsync(ct);

            // deletes of licenses owned by the entitlee and entitlements for those licenses have no concurrent concerns
            await dbContext.SteamLicenseEntitlements
                .Where(entitlement => entitlement.EntitledAccountId == context.AccountId)
                .Where(entitlement => entitlement.LastSeen < seenTime)
                .ExecuteDeleteAsync(ct);
            await dbContext.SteamLicenses
                .Where(license => license.OwnerAccountId == context.AccountId)
                .Where(license => license.LastSeen < seenTime)
                .ExecuteDeleteAsync(ct);
        }
        return;

        async Task EnsureAccount(HazeDbContext dbContext, SteamID accountId, CancellationToken ct = default)
        {
            var dbAccount = await dbContext.SteamAccounts.FindOrCreateAsync(accountId, ct);
        }

        async Task EnsurePackage(HazeDbContext dbContext, uint packageId, CancellationToken ct = default)
        {
            var dbPackage = await dbContext.SteamPackages.FindOrCreateAsync(packageId, ct);
        }

        async Task UpsertLicense(HazeDbContext dbContext, SteamApps.LicenseListCallback.License license, CancellationToken ct = default)
        {
            var dbLicense = await dbContext.SteamLicenses.FindOrCreateAsync(license, context.AccountId, ct);
            dbLicense.LastSeen = seenTime;
        }

        async Task UpsertEntitlement(HazeDbContext dbContext, SteamApps.LicenseListCallback.License license, CancellationToken ct = default)
        {
            var dbEntitlement = await dbContext.SteamLicenseEntitlements.FindOrCreateAsync(license, context.AccountId, ct);
            dbEntitlement.LastSeen = seenTime;
        }
    }

    async Task RefreshDepots(SteamSyncRefreshContext context, CancellationToken ct = default)
    {
        context.PopulatePackageTokensFromLicenseList();
        var packageRequests = context.LicensePackageIds.Select(MakeRequestForId);
        var packageResultSet = await context.Connection.Apps.PICSGetProductInfo(apps: [], packages: packageRequests);
        if (packageResultSet.Failed) throw new Exception();  // todo: specific exception
        context.PackageInfos = [..packageResultSet.Results!.SelectMany(result => result.Packages.Values)];

        var allDepotIds = context.PackageInfos.SelectMany(package => package.KeyValues["depotids"].Children)
            .Select(child => child.AsUnsignedInteger())
            .ToHashSet();

        // inserts of depots use optimistic concurrency and should retry on unique constraint violations
        await context.DbContextFactory.ExecuteRetryingAsync(async (dbContext, ct) =>
            {
                foreach (var depotId in allDepotIds) {
                    await dbContext.SteamDepots.FindOrCreateAsync(depotId, ct);
                }
                await dbContext.SaveChangesAsync(ct);
            },
            (exception) => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation },
            6,
            ct
        );
        return;

        SteamApps.PICSRequest MakeRequestForId(uint id)
        {
            var token = context.PackageTokens.TryGetValue(id, out var maybeToken) ? maybeToken : 0;
            return new SteamApps.PICSRequest(id, token);
        }
    }

    async Task RefreshApps(SteamSyncRefreshContext context, CancellationToken ct = default)
    {
        await context.PopulateAppTokensByRequesting();
        var appRequests = context.PackageInfosAppIds.Select(MakeRequestForId);
        var appResultSet = await context.Connection.Apps.PICSGetProductInfo(apps: appRequests, packages: []);
        if (appResultSet.Failed) throw new Exception();  // todo: specific exception
        context.AppInfos = [..appResultSet.Results!.SelectMany(result => result.Apps.Values)];

        // inserts of apps use optimistic concurrency and should retry on unique constraint violations
        await context.DbContextFactory.ExecuteRetryingAsync(async (dbContext, ct) =>
            {
                foreach (var app in context.AppInfos) {
                    await UpsertApp(dbContext, app.ID);
                }
                await dbContext.SaveChangesAsync(ct);
            },
            (exception) => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation },
            6,
            ct
        );
        return;

        SteamApps.PICSRequest MakeRequestForId(uint id)
        {
            var token = context.AppTokens.TryGetValue(id, out var maybeToken) ? maybeToken : 0;
            return new SteamApps.PICSRequest(id, token);
        }

        async Task UpsertApp(HazeDbContext dbContext, uint appId, CancellationToken ct = default)
        {
            var dbApp = await dbContext.SteamApps.FindOrCreateAsync(appId, ct);
        }
    }

    async Task RefreshPackageRelations(SteamSyncRefreshContext context, CancellationToken ct = default)
    {
        if (context.PackageInfos is null) throw new InvalidOperationException();
        if (context.AppInfos is null) throw new InvalidOperationException();

        await using var dbContext = await context.DbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        var toUpdate = await context.GetPackageIdsWithChanges(dbContext, ct);

        foreach (var package in context.PackageInfos) {
            if (!toUpdate.Contains(package.ID)) continue;

            var dbPackage = await dbContext.SteamPackages
                .ForUpdate()
                .Include(p => p.Depots)
                .Include(p => p.Apps)
                .FirstOrDefaultAsync(p => p.SteamPackageId == package.ID, ct);
            Debug.Assert(dbPackage is not null);
            if (dbPackage.LastChangeNumber >= package.ChangeNumber) continue;
            dbPackage.LastChangeNumber = package.ChangeNumber;

            dbPackage.Depots.Clear();
            var depotIds = package.KeyValues["depotids"].Children
                .Select(child => child.AsUnsignedInteger());
            foreach (var depotId in depotIds) {
                var dbDepot = dbContext.SteamDepots.Local.FindEntry(depotId)?.Entity;
                dbDepot ??= await dbContext.SteamDepots.FindAsync([depotId], ct);
                Debug.Assert(dbDepot is not null);
                dbPackage.Depots.Add(dbDepot);
            }

            dbPackage.Apps.Clear();
            var appIds = package.KeyValues["appids"].Children.Select(child => child.AsUnsignedInteger());
            foreach (var appId in appIds) {
                var dbApp = dbContext.SteamApps.Local.FindEntry(appId)?.Entity;
                dbApp ??= await dbContext.SteamApps.FindAsync([appId], ct);
                Debug.Assert(dbApp is not null);
                dbPackage.Apps.Add(dbApp);
            }
        }
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
