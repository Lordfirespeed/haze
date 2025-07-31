using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

/**
 * See <a href="https://steamdb.info/faq/#package">SteamDB's definition</a>.
 */
[PrimaryKey(nameof(SteamPackageId))]
public class SteamPackage
{
    public uint SteamPackageId { get; set; }

    /**
     * Packages are associated to Steam accounts via licenses.
     */
    public ICollection<SteamLicense> Licenses { get; set; } = new List<SteamLicense>();

    /**
     * Being entitled to a license to a package grants access to all its associated <see cref="SteamApp"/> instances,
     * if any exist.
     */
    public ICollection<SteamApp> Apps { get; set; } = new List<SteamApp>();

    /**
     * Being entitled to a license to a package grants access to all its associated <see cref="SteamDepot"/> instances
     * if any exist.
     */
    public ICollection<SteamDepot> Depots { get; set; } = new List<SteamDepot>();
}
