using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

/**
 * See <a href="https://steamdb.info/faq/#depot">SteamDB's definition</a>.
 */
[PrimaryKey(nameof(SteamDepotId))]
public class SteamDepot
{
    public uint SteamDepotId { get; set; }

    /**
     * Access to a depot can be granted by potentially many packages.
     * Being entitled to a license for any containing package grants access to the depot.
     */
    [InverseProperty(nameof(SteamPackage.Depots))]
    public ICollection<SteamPackage> Packages { get; set; } = new List<SteamPackage>();

    public ICollection<SteamAppDepotConfig> Configs { get; set; } = new List<SteamAppDepotConfig>();
}
