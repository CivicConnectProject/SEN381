using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnectMvc.Data;

public static class DbInitializer
{
    public static void Initialize(CivicConnectDbContext db)
    {
        db.Database.EnsureCreated();

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User { UserId = 1, Name = "Resident", Email = "resident@example.com", Role = UserRole.Requester },
                new User { UserId = 2, Name = "Staff Member", Email = "staff@example.com", Role = UserRole.Staff },
                new User { UserId = 3, Name = "Administrator", Email = "admin@example.com", Role = UserRole.Administrator });
            db.SaveChanges();
        }

        if (!db.ServiceRequests.Any())
        {
            db.ServiceRequests.Add(new ServiceRequest
            {
                RequesterId = 1,
                Category = "Facility Fault",
                Title = "Broken classroom light",
                Description = "The light in room B12 is not working."
            });
            db.SaveChanges();
        }
    }
}
