using CivicConnectMvc.Interfaces;

namespace CivicConnectMvc.Infrastructure.Notifications;

// Adapter translates CivicConnect's notification contract into the vendor client's API shape.
public class NotificationAdapter : INotificationAdapter
{
    private readonly IExternalNotificationClient _vendorClient;

    public NotificationAdapter(IExternalNotificationClient vendorClient)
        => _vendorClient = vendorClient;

    public void Send(NotificationMessage notification)
    {
        _vendorClient.SendMessage(
            notification.Recipient,
            notification.Subject,
            notification.Message);
    }
}
