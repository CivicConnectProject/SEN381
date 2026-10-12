using CivicConnectMvc.Data;
using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnectMvc.Infrastructure.Repositories;

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly CivicConnectDbContext _db;

    public ServiceRequestRepository(CivicConnectDbContext db) => _db = db;

    public List<ServiceRequest> GetAll() =>
        _db.ServiceRequests.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToList();

    public ServiceRequest? GetById(int id) =>
        _db.ServiceRequests.Include(r => r.Requester).FirstOrDefault(r => r.RequestId == id);

    public List<User> GetAssignableStaff() =>
        _db.Users.AsNoTracking().Where(u => u.IsActive &&
            (u.Role == UserRole.Staff || u.Role == UserRole.Administrator)).ToList();

    public ServiceRequest Add(ServiceRequest request)
    {
        _db.ServiceRequests.Add(request);
        return request;
    }

    public User? GetUserById(int userId) => _db.Users.FirstOrDefault(u => u.UserId == userId);

    public void SaveChanges() => _db.SaveChanges();
}
