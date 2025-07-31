using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

/**
 * A join entity which associates an ordered list of <see cref="SteamDepot"/> instances and associated 'mounting rules'
 * to a <see cref="SteamApp"/> instance.
 */
[PrimaryKey(nameof(SteamAppId), nameof(SteamDepotId))]
[Index(nameof(MountOrder))]
public class SteamAppDepotConfig
{
    public uint SteamAppId { get; set; }

    public uint SteamDepotId { get; set; }

    public uint MountOrder { get; set; }

    public bool Optional { get; set; }

    [StringLength(16)]
    public string? Language { get; set; }

    public string[]? OperatingSystems { get; set; }

    [StringLength(16)]
    public string? OperatingSystemArch { get; set; }

    [ForeignKey(nameof(SteamAppId))]
    public SteamApp App { get; set; } = null!;

    [ForeignKey(nameof(SteamDepotId))]
    public SteamDepot Depot { get; set; } = null!;
}
