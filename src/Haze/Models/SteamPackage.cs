using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(SteamPackageId))]
public class SteamPackage
{
    public uint SteamPackageId { get; set; }
}
