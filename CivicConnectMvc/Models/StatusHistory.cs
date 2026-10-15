namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.status_history</c> table.
/// An immutable audit row written every time a request's status changes.
/// </summary>
public class StatusHistory
{
    public int StatusHistoryId { get; set; }

    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }

    /// <summary>
    /// FK to <c>users.user_id</c> (column name: <c>changed_by</c>).
    /// Stored as a plain integer; the navigation is <see cref="ChangedByUser"/>.
    /// </summary>
    public int ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }

    /// <summary>String value of the <see cref="RequestStatus"/> before the transition.</summary>
    public string PreviousStatus { get; set; } = "";

    /// <summary>String value of the <see cref="RequestStatus"/> after the transition.</summary>
    public string NewStatus { get; set; } = "";

    /// <summary>Timestamp set by the database DEFAULT CURRENT_TIMESTAMP.</summary>
    public DateTime ChangedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
