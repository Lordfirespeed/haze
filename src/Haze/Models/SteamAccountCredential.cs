using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(CredentialId))]
public class SteamAccountCredential
{
    public uint CredentialId { get; set; }

    [Required]
    public SteamAccountCredentialUsage Usage { get; set; }

    [Required]
    [StringLength(1024)]
    public string SteamAccessToken { get; set; } = null!;

    [Required]
    [StringLength(1024)]
    public string SteamRefreshToken { get; set; } = null!;

    [Required]
    public DateTime RefreshedAt { get; set; }

    [Required]
    public DateTime RefreshAttemptedAt { get; set; }

    [Required]
    public SteamID SteamAccountId { get; set; } = null!;

    [ForeignKey(nameof(SteamAccountId))]
    public SteamAccount Account { get; set; } = null!;
}
