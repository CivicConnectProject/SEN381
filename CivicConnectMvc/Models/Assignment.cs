namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.assignments</c> table.
/// One row per assignment action; <see cref="Active"/> = true marks the current active assignment.
/// </summary>
public class Assignment
{
    public int AssignmentId { get; set; }

    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }

    public int StaffId { get; set; }
    public User? Staff { get; set; }

    /// <summary>Timestamp set by the database DEFAULT CURRENT_TIMESTAMP.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    /// <summary>True for the current active assignment; false for historical superseded rows.</summary>
    public bool Active { get; set; } = true;
}
