using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(ClientId))]
public class HazeClient
{
    [Required]
    [StringLength(64)]
    public required string ClientId { get; set; }

    [Required]
    [StringLength(64)]
    public required string ClientSecret { get; set; }

    public int QueuePriority { get; set; } = 0;

    public ICollection<HazeClientSession> ClientSessions { get; } = new List<HazeClientSession>();
}
