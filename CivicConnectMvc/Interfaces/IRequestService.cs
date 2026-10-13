using CivicConnectMvc.Models;

namespace CivicConnectMvc.Interfaces;

public interface IRequestService
{
    List<ServiceRequest> GetAll();
    ServiceRequest? GetById(int id);
    List<Category> GetCategories();
    ServiceRequest Create(ServiceRequestViewModel model, int requesterId = 1);
    void Assign(int requestId, int staffId);
    void MoveToNextStatus(int requestId);
    List<User> GetAssignableStaff();
}
