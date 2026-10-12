using CivicConnectMvc.Models;

namespace CivicConnectMvc.Interfaces;

public interface IServiceRequestRepository
{
    List<ServiceRequest> GetAll();
    ServiceRequest? GetById(int id);
    List<User> GetAssignableStaff();
    ServiceRequest Add(ServiceRequest request);
    User? GetUserById(int userId);
    void SaveChanges();
}
