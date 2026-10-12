using CivicConnectMvc.Interfaces;

namespace CivicConnectMvc.Infrastructure.Notifications;

// A safe local stand-in for an external notification vendor.
// Replace this class with a real provider client when an external service is selected.
public class DemoNotificationClient : IExternalNotificationClient
{
    public void SendMessage(string recipient, string title, string body)
    {
        Console.WriteLine($"[Notification vendor] To: {recipient} | {title} | {body}");
    }
}
