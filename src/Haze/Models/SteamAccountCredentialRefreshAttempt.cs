using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SteamKit2;

namespace Haze.Models;

[PrimaryKey(nameof(AttemptId))]
public class SteamAccountCredentialRefreshAttempt
{
    public ulong AttemptId { get; set; }

    [Required]
    public DateTime AttemptedAt { get; set; }

    [Required]
    public EResult LogOnResult { get; set; }

    [Required]
    public EResult LogOnExtendedResult { get; set; }

    [Required]
    public bool AccessTokenRefreshed { get; set; }

    [Required]
    public bool RefreshTokenRefreshed { get; set; }

    [Required]
    public uint CredentialId { get; set; }

    [ForeignKey(nameof(CredentialId))]
    public SteamAccountCredential Credential { get; set; } = null!;
}
