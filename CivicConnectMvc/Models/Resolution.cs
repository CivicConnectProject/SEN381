namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.resolutions</c> table.
/// At most one resolution per service request (UNIQUE constraint on <c>request_id</c>).
/// </summary>
public class Resolution
{
    public int ResolutionId { get; set; }

    /// <summary>
    /// FK to <c>service_requests.request_id</c>.
    /// The UNIQUE constraint in the database enforces one resolution per request.
    /// </summary>
    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }

    /// <summary>FK to <c>users.user_id</c> (column name: <c>resolved_by</c>).</summary>
    public int ResolvedBy { get; set; }
    public User? ResolvedByUser { get; set; }

    /// <summary>Free-text resolution details (mapped to PostgreSQL <c>text</c>).</summary>
    public string Details { get; set; } = "";

    /// <summary>Timestamp set by the database DEFAULT CURRENT_TIMESTAMP.</summary>
    public DateTime ResolvedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
