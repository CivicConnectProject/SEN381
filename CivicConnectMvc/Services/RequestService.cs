using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;

namespace CivicConnectMvc.Services;

public class RequestService : IRequestService, IRequestSubject
{
    private readonly IServiceRequestRepository _repository;
    private readonly List<IRequestObserver> _observers = [];

    public RequestService(IServiceRequestRepository repository, IEnumerable<IRequestObserver> observers)
    {
        _repository = repository;
        _observers.AddRange(observers);
    }

    public void Attach(IRequestObserver observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer);
    }

    public void Detach(IRequestObserver observer) => _observers.Remove(observer);

    public void Notify(ServiceRequest request, string action)
    {
        foreach (var observer in _observers.ToList())
            observer.OnRequestChanged(request, action);
    }

    public List<ServiceRequest> GetAll() => _repository.GetAll();

    public ServiceRequest? GetById(int id) => _repository.GetById(id);

    public ServiceRequest Create(ServiceRequestViewModel model, int requesterId = 1)
    {
        var requester = _repository.GetUserById(requesterId)
            ?? throw new KeyNotFoundException("Requester was not found.");

        var request = new ServiceRequest
        {
            RequesterId = requesterId,
            Requester = requester,
            Title = model.Title,
            Description = model.Description,
            Category = model.Category
        };

        _repository.Add(request);
        _repository.SaveChanges();
        Notify(request, "A new service request was created");
        return request;
    }

    public void Assign(int requestId, int staffId)
    {
        var request = GetRequiredRequest(requestId);
        var staff = _repository.GetUserById(staffId)
            ?? throw new KeyNotFoundException("Staff member was not found.");

        request.AssignTo(staff);
        _repository.SaveChanges();
        Notify(request, $"Request was assigned to {staff.Name}");
    }

    public void MoveToNextStatus(int requestId)
    {
        var request = GetRequiredRequest(requestId);
        var next = request.CurrentStatus switch
        {
            RequestStatus.Assigned => RequestStatus.InProgress,
            RequestStatus.InProgress => RequestStatus.Resolved,
            RequestStatus.Resolved => RequestStatus.Closed,
            _ => throw new InvalidOperationException("The request must be assigned before its work can progress.")
        };

        request.ChangeStatus(next);
        _repository.SaveChanges();
        Notify(request, $"Request status changed to {next}");
    }

    public List<User> GetAssignableStaff() => _repository.GetAssignableStaff();

    private ServiceRequest GetRequiredRequest(int id) =>
        GetById(id) ?? throw new KeyNotFoundException("Service request was not found.");
}
