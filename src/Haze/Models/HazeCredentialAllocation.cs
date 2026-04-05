using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(AllocationId))]
public class HazeCredentialAllocation
{
    public ulong AllocationId { get; set; }

    [Required]
    public uint CredentialId { get; set; }

    [Required]
    public ulong JobId { get; set; }

    [ForeignKey(nameof(CredentialId))]
    [InverseProperty(nameof(SteamAccountCredential.Allocations))]
    public SteamAccountCredential Credential { get; set; } = null!;

    [ForeignKey(nameof(JobId))]
    public HazeClientJob Job { get; set; } = null!;
}
