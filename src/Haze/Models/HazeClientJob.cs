using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Haze.Models;

[PrimaryKey(nameof(JobId))]
public class HazeClientJob
{
    public ulong JobId { get; set; }

    [Required]
    [StringLength(64)]
    public required string OwnerSessionId { get; set; }

    [ForeignKey(nameof(OwnerSessionId))]
    [InverseProperty(nameof(HazeClientSession.Job))]
    public HazeClientSession OwnerSession { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public HazeClientJobState State { get; set; }

    /**
     * <summary>
     * This reason code can be used to identify why a pending job has not yet been started.
     * There may be multiple reasons why a job cannot start, but only the first reason encountered
     * by the attempted scheduling method will be displayed.
     * </summary>
     * <seealso cref="HazeClientJobState.Pending"/>
     */
    [StringLength(64)]
    public string? PendingReasonCode { get; set; }
}
