using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(EntitledAccountId), nameof(LicenseOwnerAccountId), nameof(LicensePackageId))]
public class SteamLicenseEntitlement
{
    [Required]
    public SteamID EntitledAccountId { get; set; } = null!;

    [Required]
    public SteamID LicenseOwnerAccountId { get; set; } = null!;

    [Required]
    public uint LicensePackageId { get; set; }

    [ForeignKey(nameof(EntitledAccountId))]
    public SteamAccount EntitledAccount { get; set; } = null!;

    [ForeignKey($"{nameof(LicenseOwnerAccountId)},{nameof(LicensePackageId)}")]
    public SteamLicense License { get; set; } = null!;
}
