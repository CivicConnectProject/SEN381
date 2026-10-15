namespace CivicConnectMvc.Models;

/// <summary>
/// Maps to the M2 <c>public.users</c> table.
/// Columns: user_id, name, email (UNIQUE), role.
/// Note: the M2 schema does not include an is_active column.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Returns true when the user's role permits processing requests.
    /// The M2 schema does not store an is_active flag; all persisted users are treated as active.
    /// </summary>
    public bool CanProcessRequests() =>
        Role == UserRole.Staff || Role == UserRole.Administrator;

    public bool CanViewReports() =>
        Role == UserRole.Management || Role == UserRole.Administrator;
}
