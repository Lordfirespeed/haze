using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(SessionId))]
public class HazeClientSession
{
    public Guid SessionId { get; set; }

    [Required]
    public string ClientId { get; set; } = null!;

    [ForeignKey(nameof(ClientId))]
    public HazeClient Client { get; set; } = null!;
}
