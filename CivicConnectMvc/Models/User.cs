namespace CivicConnectMvc.Models;

public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public bool CanProcessRequests() =>
        IsActive && (Role == UserRole.Staff || Role == UserRole.Administrator);

    public bool CanViewReports() =>
        Role == UserRole.Management || Role == UserRole.Administrator;
}
