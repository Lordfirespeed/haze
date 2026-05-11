using System;
using SteamKit2;

namespace Haze.Steam;

public static class LicenseExtensions
{
    extension(SteamApps.LicenseListCallback.License license)
    {
        public bool IsOwnedBy(SteamID userId) => license.OwnerAccountID == userId.AccountID;

        public bool IsFamilyShared() => license.PaymentMethod is EPaymentMethod.FamilyGroup;

        /**
         * This is a bit of guesswork.
         * <seealso href="https://partner.steamgames.com/doc/sitelicense/licensees"/>
         */
        public bool IsCyberCafeShared() => license.PaymentMethod is EPaymentMethod.CafeFunded;

        public SteamID GetOwnerFullId(SteamID entitledUserId)
        {
            if (license.IsOwnedBy(entitledUserId)) return entitledUserId;
            if (license.IsFamilyShared())
                return new SteamID(license.OwnerAccountID, entitledUserId.AccountUniverse, EAccountType.Individual);
            if (license.IsCyberCafeShared())
                return new SteamID(license.OwnerAccountID, entitledUserId.AccountUniverse, EAccountType.Multiseat);
            throw new InvalidOperationException(
                $"Can't determine full owner SteamID for license, package ID {license.PackageID}, entitlee {entitledUserId.AccountID}, owner {license.OwnerAccountID}, payment type {license.PaymentMethod}"
            );
        }
    }
}
