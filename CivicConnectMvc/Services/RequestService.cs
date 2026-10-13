using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;
using Microsoft.EntityFrameworkCore;

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

    public List<Category> GetCategories() => _repository.GetCategories();

    public ServiceRequest Create(ServiceRequestViewModel model, int requesterId = 1)
    {
        var requester = _repository.GetUserById(requesterId)
            ?? throw new KeyNotFoundException("Requester was not found.");
        var category = _repository.GetCategoryById(model.CategoryId)
            ?? throw new KeyNotFoundException("Category was not found.");

        var request = new ServiceRequest
        {
            RequesterId = requesterId,
            Requester = requester,
            CategoryId = category.CategoryId,
            Category = category,
            Title = model.Title,
            Description = model.Description
        };

        request.RequestActions.Add(new RequestAction
        {
            RequestId = request.RequestId,
            Request = request,
            UserId = requesterId,
            Details = "Service request submitted"
        });

        _repository.Add(request);
        SaveChangesSafely();
        Notify(request, "A new service request was created");
        return request;
    }

    public void Assign(int requestId, int staffId)
    {
        var request = GetRequiredRequest(requestId);
        var staff = _repository.GetUserById(staffId)
            ?? throw new KeyNotFoundException("Staff member was not found.");

        var previousStatus = request.CurrentStatus;
        request.AssignTo(staff);
        request.StatusHistories.Add(new StatusHistory
        {
            RequestId = request.RequestId,
            Request = request,
            ChangedBy = staffId,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = request.CurrentStatus.ToString()
        });
        request.RequestActions.Add(new RequestAction
        {
            RequestId = request.RequestId,
            Request = request,
            UserId = staffId,
            Details = $"Request assigned to {staff.Name}"
        });
        SaveChangesSafely();
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

        var previousStatus = request.CurrentStatus;
        request.ChangeStatus(next);
        request.StatusHistories.Add(new StatusHistory
        {
            RequestId = request.RequestId,
            Request = request,
            ChangedBy = request.RequesterId,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = next.ToString()
        });
        request.RequestActions.Add(new RequestAction
        {
            RequestId = request.RequestId,
            Request = request,
            UserId = request.RequesterId,
            Details = $"Request status changed to {next}"
        });
        if (next == RequestStatus.Resolved)
        {
            request.Resolution = new Resolution
            {
                RequestId = request.RequestId,
                Request = request,
                ResolvedBy = request.RequesterId,
                Details = "Request marked as resolved"
            };
        }

        SaveChangesSafely();
        Notify(request, $"Request status changed to {next}");
    }

    public List<User> GetAssignableStaff() => _repository.GetAssignableStaff();

    private ServiceRequest GetRequiredRequest(int id) =>
        GetById(id) ?? throw new KeyNotFoundException("Service request was not found.");

    private void SaveChangesSafely()
    {
        try
        {
            _repository.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "This request was changed by another user. Reload it and try again.");
        }
    }
}
