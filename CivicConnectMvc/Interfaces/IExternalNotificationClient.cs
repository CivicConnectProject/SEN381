namespace CivicConnectMvc.Interfaces;

// Represents an external provider's different API shape.
public interface IExternalNotificationClient
{
    void SendMessage(string recipient, string title, string body);
}
