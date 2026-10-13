namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.request_actions</c> table.
/// Records any staff action taken on a service request (comments, notes, updates).
/// </summary>
public class RequestAction
{
    public int ActionId { get; set; }

    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }

    /// <summary>FK to <c>users.user_id</c> – the user who performed the action.</summary>
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Free-text description of the action taken (mapped to PostgreSQL <c>text</c>).</summary>
    public string Details { get; set; } = "";

    /// <summary>Timestamp set by the database DEFAULT CURRENT_TIMESTAMP.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
