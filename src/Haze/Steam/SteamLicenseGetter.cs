using System;
using System.Threading.Tasks;
using Haze.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteamKit2;

namespace Haze.Steam;

public class SteamLicenseGetter(IDbContextFactory<HazeDbContext> dbContextFactory, ILogger<SteamLicenseGetter> logger)
{
    private SteamAccount _account = null!;

    public async Task Foo()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var cred = await dbContext.SteamAccountCredentials
            .Include(cred => cred.Account)
            .FirstOrDefaultAsync(cred => cred.CredentialId == 2);
        if (cred is null) throw new InvalidOperationException();
        _account = cred.Account;

        await using var connection = new SteamConnection(logger);
        await connection.Connect();

        connection.DbAuth(cred);

        using var onLicenseListCallback = connection.Manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
        await connection.LogOn();
        await Task.Delay(1000);
    }

    public async Task OnLicenseList(SteamApps.LicenseListCallback callback)
    {
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

            logger.LogDebug($"license for package {license.PackageID}, owner {license.OwnerAccountID}, {license.PaymentMethod}");
        }
    }
}
