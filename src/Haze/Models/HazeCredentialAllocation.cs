using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(AllocationId))]
public class HazeCredentialAllocation
{
    public long AllocationId { get; set; }

    [Required]
    public int CredentialId { get; set; }

    [Required]
    public long JobId { get; set; }

    [ForeignKey(nameof(CredentialId))]
    [InverseProperty(nameof(SteamAccountCredential.Allocations))]
    public SteamAccountCredential Credential { get; set; } = null!;

    [ForeignKey(nameof(JobId))]
    public HazeClientJob Job { get; set; } = null!;
}
