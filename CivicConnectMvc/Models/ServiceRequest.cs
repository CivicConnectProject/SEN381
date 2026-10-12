namespace CivicConnectMvc.Models;

public class ServiceRequest
{
    public int RequestId { get; set; }
    public int RequesterId { get; set; }
    public User? Requester { get; set; }
    public string Category { get; set; } = "Other";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public RequestStatus CurrentStatus { get; private set; } = RequestStatus.Submitted;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public int? AssignedStaffId { get; private set; }

    public void ChangeStatus(RequestStatus newStatus)
    {
        if (!CanTransition(CurrentStatus, newStatus))
            throw new InvalidOperationException($"Cannot change status from {CurrentStatus} to {newStatus}.");

        CurrentStatus = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTo(User staff)
    {
        if (!staff.CanProcessRequests())
            throw new InvalidOperationException("Only an active staff member or administrator can be assigned.");

        if (CurrentStatus != RequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be assigned.");

        AssignedStaffId = staff.UserId;
        CurrentStatus = RequestStatus.Assigned;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReassignTo(User staff)
    {
        if (!staff.CanProcessRequests())
            throw new InvalidOperationException("Only an active staff member or administrator can be assigned.");

        if (CurrentStatus is RequestStatus.Resolved or RequestStatus.Closed)
            throw new InvalidOperationException("Resolved or closed requests cannot be reassigned.");

        AssignedStaffId = staff.UserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public static bool CanTransition(RequestStatus current, RequestStatus next) => current switch
    {
        RequestStatus.Submitted => next == RequestStatus.Assigned,
        RequestStatus.Assigned => next == RequestStatus.InProgress,
        RequestStatus.InProgress => next == RequestStatus.Resolved,
        RequestStatus.Resolved => next == RequestStatus.Closed,
        _ => false
    };
}
