namespace CivicConnectMvc.Interfaces;

public record NotificationMessage(string Recipient, string Subject, string Message);

public interface INotificationAdapter
{
    void Send(NotificationMessage notification);
}
