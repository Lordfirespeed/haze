using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(AttemptId))]
public class SteamAccountProductInfoRefreshAttempt
{
    public ulong AttemptId { get; set; }

    [Required]
    public DateTime AttemptStartedAt { get; set; }

    [Required]
    public DateTime AttemptCompletedAt { get; set; }

    [Required]
    public uint LastChangeNumber { get; set; }

    [Required]
    public SteamID SteamAccountId { get; set; } = null!;

    [ForeignKey(nameof(SteamAccountId))]
    public SteamAccount Account { get; set; } = null!;
}
