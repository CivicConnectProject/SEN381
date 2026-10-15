namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.service_requests</c> table.
/// Columns: request_id, requester_id, category_id, title (varchar 200),
/// description (text), current_status (varchar 50), created_at, updated_at, version.
/// </summary>
public class ServiceRequest
{
    public int RequestId { get; set; }

    // ── Requester (FK → users.user_id) ──────────────────────────────────────
    public int RequesterId { get; set; }
    public User? Requester { get; set; }

    // ── Category (FK → categories.category_id) ───────────────────────────────
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // ── Core fields ──────────────────────────────────────────────────────────
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public RequestStatus? CurrentStatus =>
        TryGetKnownStatus(out var status) ? status : null;
    public string CurrentStatusText { get; private set; } = nameof(RequestStatus.Submitted);
    public DateTime CreatedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public DateTime UpdatedAt { get; private set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    /// <summary>
    /// Optimistic-concurrency version counter (DEFAULT 1 in the database).
    /// Incremented by the service layer on every write.
    /// </summary>
    public int Version { get; private set; } = 1;

    // ── Navigation properties for related M2 tables ───────────────────────────
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    public ICollection<RequestAction> RequestActions { get; set; } = new List<RequestAction>();

    /// <summary>
    /// At most one resolution per request (UNIQUE constraint on resolutions.request_id).
    /// </summary>
    public Resolution? Resolution { get; set; }

    // ── View-compatibility helpers ────────────────────────────────────────────

    /// <summary>
    /// Returns the category name for display. Falls back to the raw CategoryId string
    /// when the Category navigation has not been eagerly loaded.
    /// Used by the Details and Index views via @Model.Category (Razor resolves the property).
    /// </summary>
    public string CategoryName =>
        Category?.Name ?? $"Category #{CategoryId}";

    /// <summary>
    /// Returns the staff_id of the currently active assignment, or null when unassigned.
    /// Keeps the Details view expression <c>@(Model.AssignedStaffId?.ToString() ?? "Not assigned")</c>
    /// working without storing a denormalised column on this row.
    /// </summary>
    public int? AssignedStaffId =>
        Assignments.FirstOrDefault(a => a.Active)?.StaffId;

    public bool CanAdvanceStatus() =>
        TryGetKnownStatus(out var status) &&
        status is RequestStatus.Assigned or RequestStatus.InProgress or RequestStatus.Resolved;

    public bool TryGetKnownStatus(out RequestStatus status) =>
        Enum.TryParse(CurrentStatusText, ignoreCase: false, out status) &&
        Enum.IsDefined(status);

    // ── Domain methods ────────────────────────────────────────────────────────

    public void ChangeStatus(RequestStatus newStatus)
    {
        if (!TryGetKnownStatus(out var currentStatus))
            throw new InvalidOperationException(
                $"Cannot change unsupported request status '{CurrentStatusText}'.");

        if (!CanTransition(currentStatus, newStatus))
            throw new InvalidOperationException(
                $"Cannot change status from {currentStatus} to {newStatus}.");

        CurrentStatusText = newStatus.ToString();
        UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Version++;
    }

    /// <summary>
    /// Validates that <paramref name="staff"/> is permitted to be assigned and that the
    /// request is in the correct state. The caller (service layer) is responsible for
    /// persisting the <see cref="Assignment"/> row and recording a <see cref="StatusHistory"/> entry.
    /// </summary>
    public void AssignTo(User staff)
    {
        if (!staff.CanProcessRequests())
            throw new InvalidOperationException(
                "Only a staff member or administrator can be assigned.");

        if (CurrentStatusText != nameof(RequestStatus.Submitted))
            throw new InvalidOperationException(
                "Only submitted requests can be assigned.");

        CurrentStatusText = nameof(RequestStatus.Assigned);
        UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Version++;
        Assignments.Add(new Assignment
        {
            RequestId = RequestId,
            Request = this,
            StaffId = staff.UserId,
            Staff = staff
        });
    }

    /// <summary>
    /// Validates a reassignment on an in-progress request. The caller is responsible
    /// for persisting the new <see cref="Assignment"/> row.
    /// </summary>
    public void ReassignTo(User staff)
    {
        if (!staff.CanProcessRequests())
            throw new InvalidOperationException(
                "Only a staff member or administrator can be assigned.");

        if (!TryGetKnownStatus(out var currentStatus))
            throw new InvalidOperationException(
                $"Cannot reassign a request with unsupported status '{CurrentStatusText}'.");

        if (currentStatus is RequestStatus.Resolved or RequestStatus.Closed)
            throw new InvalidOperationException(
                "Resolved or closed requests cannot be reassigned.");

        foreach (var assignment in Assignments.Where(a => a.Active))
            assignment.Active = false;

        UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Version++;
        Assignments.Add(new Assignment
        {
            RequestId = RequestId,
            Request = this,
            StaffId = staff.UserId,
            Staff = staff
        });
    }

    public static bool CanTransition(RequestStatus current, RequestStatus next) => current switch
    {
        RequestStatus.Submitted  => next == RequestStatus.Assigned,
        RequestStatus.Assigned   => next == RequestStatus.InProgress,
        RequestStatus.InProgress => next == RequestStatus.Resolved,
        RequestStatus.Resolved   => next == RequestStatus.Closed,
        _                        => false
    };
}
