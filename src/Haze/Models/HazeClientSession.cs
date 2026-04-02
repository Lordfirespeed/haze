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
}
