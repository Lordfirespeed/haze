using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(SteamAccountId))]
public class SteamAccount
{
    public SteamID SteamAccountId { get; set; } = null!;

    [Required]
    [StringLength(32)]
    public string SteamAccountName { get; set; } = null!;

    public ICollection<SteamAccountCredential> Credentials { get; } = new List<SteamAccountCredential>();

    /**
     * All licenses this account owns (a weak subset of all licenses it has access to).
     */
    [InverseProperty(nameof(SteamLicense.OwnerAccount))]
    public ICollection<SteamLicense> OwnedLicenses { get; set; } = new List<SteamLicense>();

    /**
     * All licenses this account has access to, including licenses owned by other accounts which are shared with this
     * account.
     */
    [InverseProperty(nameof(SteamLicense.EntitledAccounts))]
    public ICollection<SteamLicense> Licenses { get; set; } = new List<SteamLicense>();

    /**
     * A <see cref="SteamLicenseEntitlement"/> is a join entity which associates <see cref="SteamAccount"/> instances
     * to <see cref="SteamLicense"/> instances they have access to.
     */
    public ICollection<SteamLicenseEntitlement> Entitlements { get; set; } = new List<SteamLicenseEntitlement>();
}
