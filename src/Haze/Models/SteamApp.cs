using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

/**
 * See <a href="https://steamdb.info/faq/#app">SteamDB's definition</a>.
 */
[PrimaryKey(nameof(SteamAppId))]
public class SteamApp
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public uint SteamAppId { get; set; }

    [Required]
    public uint LastChangeNumber { get; set; }

    /**
     * Access to an app can be granted by potentially many packages.
     * Being entitled to a license for any containing package grants access to the app.
     */
    [InverseProperty(nameof(SteamPackage.Apps))]
    public ICollection<SteamPackage> Packages { get; set; } = new List<SteamPackage>();

    /**
     * Installing an app involves 'mounting' (downloading and installing) various depots, following
     * 'depot mounting rules' for each.
     */
    public ICollection<SteamAppDepotConfig> DepotConfigs { get; set; } = new List<SteamAppDepotConfig>();
}
