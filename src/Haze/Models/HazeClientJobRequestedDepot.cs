using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

/**
 * A join entity which associates a set of <see cref="SteamDepot"/> instances
 * to a <see cref="HazeClientJob"/> instance.
 */
[PrimaryKey(nameof(JobId), nameof(SteamDepotId))]
public class HazeClientJobRequestedDepot
{
    public ulong JobId { get; set; }

    public uint SteamDepotId { get; set; }

    [ForeignKey(nameof(JobId))]
    public HazeClientJob Job { get; set; } = null!;

    [ForeignKey(nameof(SteamDepotId))]
    public SteamDepot SteamDepot { get; set; } = null!;
}
