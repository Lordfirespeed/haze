using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(ClientId))]
public class HazeClient
{
    public required string ClientId { get; set; }

    public int QueuePriority { get; set; } = 0;

    public ICollection<HazeClientSession> ClientSessions { get; } = new List<HazeClientSession>();
}
