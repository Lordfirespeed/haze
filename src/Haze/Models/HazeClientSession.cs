using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(SessionId))]
public class HazeClientSession
{
    [StringLength(64)]
    public required string SessionId { get; set; }

    [StringLength(64)]
    public string? ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public HazeClient? Client { get; set; }

    /**
     * As long as the WebSocket connection is alive, <see cref="DisconnectAt"/> should be <c>null</c>.
     * If the connection is dropped, <see cref="DisconnectAt"/> should be set to the time of disconnection.
     */
    public DateTime? DisconnectAt { get; set; }

    public bool Connected => DisconnectAt is null;
}
