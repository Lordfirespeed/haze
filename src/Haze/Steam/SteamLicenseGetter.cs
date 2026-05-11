using System;
using System.Collections.ObjectModel;
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

namespace Haze.Steam;

public class SteamLicenseGetter(IDbContextFactory<HazeDbContext> dbContextFactory, ILogger<SteamLicenseGetter> logger)
{
    private SteamAccount _account = null!;
    private ReadOnlyCollection<SteamApps.LicenseListCallback.License> _lastLicenseList = null!;
    private readonly TaskCompletionSource _firstLicenseListTcs = new();

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

        using var onLicenseListCallback = connection.Manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
        await connection.LogOn();

        await _firstLicenseListTcs.Task;

        // ref: https://github.com/SteamRE/SteamKit/issues/531#issuecomment-377963355
        var appRequest = new SteamApps.PICSRequest(1771300); // KCD2 app
        var packageRequest = new SteamApps.PICSRequest(710154); // lethal company store package
        var resultSet = await connection.Apps.PICSGetProductInfo(
            apps: [appRequest],
            packages: [packageRequest]
        );
        Debug.Assert(resultSet.Results is not null);
        foreach (var (appId, appInfo) in resultSet.Results.SelectMany(result => result.Apps))
        {
            logger.LogInformation($"app {appId}, only public: {appInfo.OnlyPublic}, {await Checksum.Sha256SumObject(appInfo, ct)}");
        }

        foreach (var (packageId, packageInfo) in resultSet.Results.SelectMany(result => result.Packages))
        {
            logger.LogInformation($"package {packageId}, only public: {packageInfo.OnlyPublic}, {await Checksum.Sha256SumObject(packageInfo, ct)}");
        }
    }

    public async Task OnLicenseList(SteamApps.LicenseListCallback callback)
    {
        logger.LogInformation($"Got a license list at {DateTime.Now} (local time)");
        if (callback.Result is not EResult.OK) throw new Exception();
        _lastLicenseList = callback.LicenseList;
        if (!_firstLicenseListTcs.Task.IsCompleted) _firstLicenseListTcs.SetResult();

        foreach (var license in callback.LicenseList)
        {
            if (license.PaymentMethod is EPaymentMethod.FamilyGroup)
            {
                var ownerId = new SteamID(license.OwnerAccountID, _account.SteamAccountId.AccountUniverse, EAccountType.Individual);
                // do stuff with proper owner ID
                continue;
            }

            if (license.PaymentMethod is EPaymentMethod.CafeFunded) // this is a total guess!
            {
                var ownerId = new SteamID(license.OwnerAccountID, _account.SteamAccountId.AccountUniverse, EAccountType.Multiseat);
                // do stuff with proper owner ID
                continue;
            }

            if (license.OwnerAccountID != _account.SteamAccountId.AccountID)
            {
                logger.LogDebug($"Shared license for package {license.PackageID} is owned by {license.OwnerAccountID} (expected {_account.SteamAccountId.AccountID}) with payment method {license.PaymentMethod}");
                continue;
            }

            // logger.LogDebug($"license for package {license.PackageID}, owner {license.OwnerAccountID}, {license.PaymentMethod}");
        }
    }
}
