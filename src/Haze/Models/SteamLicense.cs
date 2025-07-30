using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(OwnerAccountId), nameof(PackageId))]

public class SteamLicense
{
    [Required]
    public SteamID OwnerAccountId { get; set; } = null!;

    [Required]
    public uint PackageId { get; set; }

    [ForeignKey(nameof(OwnerAccountId))]
    [InverseProperty(nameof(SteamAccount.OwnedLicenses))]
    public SteamAccount OwnerAccount { get; set; } = null!;

    [ForeignKey(nameof(PackageId))]
    public SteamPackage Package { get; set; } = null!;

    [InverseProperty(nameof(SteamAccount.Licenses))]
    public ICollection<SteamAccount> EntitledAccounts { get; set; } = new List<SteamAccount>();

    public ICollection<SteamLicenseEntitlement> Entitlements { get; set; } = new List<SteamLicenseEntitlement>();
}
