using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;

namespace CivicConnectMvc.Infrastructure.Notifications;

// Observer reacts to service-request changes without putting notification code in the domain model.
public class RequestNotificationObserver : IRequestObserver
{
    private readonly INotificationAdapter _notificationAdapter;

    public RequestNotificationObserver(INotificationAdapter notificationAdapter)
        => _notificationAdapter = notificationAdapter;

    public void OnRequestChanged(ServiceRequest request, string action)
    {
        var recipient = request.Requester?.Email ?? "resident@example.com";
        var notification = new NotificationMessage(
            recipient,
            $"CivicConnect request #{request.RequestId} updated",
            $"{action}. Current status: {request.CurrentStatusText}.");

        _notificationAdapter.Send(notification);
    }
}
