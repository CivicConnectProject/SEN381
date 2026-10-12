using CivicConnectMvc.Infrastructure.Notifications;
using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Models;

namespace CivicConnectMvc.Tests;

public static class ServiceRequestTests
{
    public static void New_Request_Starts_As_Submitted()
    {
        var request = new ServiceRequest();
        Check.Equal(RequestStatus.Submitted, request.CurrentStatus);
    }

    public static void Valid_Status_Transition_Is_Allowed()
    {
        var request = new ServiceRequest();
        request.ChangeStatus(RequestStatus.Assigned);
        Check.Equal(RequestStatus.Assigned, request.CurrentStatus);
    }

    public static void Invalid_Status_Transition_Is_Rejected()
    {
        var request = new ServiceRequest();
        Check.Throws<InvalidOperationException>(() => request.ChangeStatus(RequestStatus.Closed));
    }

    public static void Active_Staff_Can_Be_Assigned()
    {
        var request = new ServiceRequest();
        var staff = new User { UserId = 2, Role = UserRole.Staff, IsActive = true };

        request.AssignTo(staff);

        Check.Equal(RequestStatus.Assigned, request.CurrentStatus);
        Check.Equal(2, request.AssignedStaffId);
    }

    public static void Requester_Cannot_Be_Assigned()
    {
        var request = new ServiceRequest();
        var requester = new User { UserId = 1, Role = UserRole.Requester, IsActive = true };

        Check.Throws<InvalidOperationException>(() => request.AssignTo(requester));
    }

    public static void Closed_Request_Cannot_Be_Reassigned()
    {
        var request = new ServiceRequest();
        var staff = new User { UserId = 2, Role = UserRole.Staff, IsActive = true };

        request.AssignTo(staff);
        request.ChangeStatus(RequestStatus.InProgress);
        request.ChangeStatus(RequestStatus.Resolved);
        request.ChangeStatus(RequestStatus.Closed);

        Check.Throws<InvalidOperationException>(() => request.ReassignTo(staff));
    }

    public static void Adapter_Translates_Notification_To_Vendor_Client()
    {
        var client = new FakeVendorClient();
        var adapter = new NotificationAdapter(client);

        adapter.Send(new NotificationMessage("resident@example.com", "Test", "Hello"));

        Check.Equal("resident@example.com", client.Recipient);
        Check.Equal("Test", client.Title);
        Check.Equal("Hello", client.Body);
    }

    public static void Observer_Receives_Request_Change()
    {
        var adapter = new FakeNotificationAdapter();
        var observer = new RequestNotificationObserver(adapter);
        var request = new ServiceRequest { RequestId = 7 };

        observer.OnRequestChanged(request, "Request status changed");

        Check.NotNull(adapter.LastNotification);
        Check.Contains("Request status changed", adapter.LastNotification!.Message);
    }

    private sealed class FakeVendorClient : IExternalNotificationClient
    {
        public string? Recipient { get; private set; }
        public string? Title { get; private set; }
        public string? Body { get; private set; }

        public void SendMessage(string recipient, string title, string body)
        {
            Recipient = recipient;
            Title = title;
            Body = body;
        }
    }

    private sealed class FakeNotificationAdapter : INotificationAdapter
    {
        public NotificationMessage? LastNotification { get; private set; }

        public void Send(NotificationMessage notification)
            => LastNotification = notification;
    }

    private static class Check
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Expected '{expected}' but got '{actual}'.");
        }

        public static void NotNull(object? value)
        {
            if (value is null)
                throw new Exception("Expected a value, but got null.");
        }

        public static void Contains(string expected, string actual)
        {
            if (!actual.Contains(expected, StringComparison.Ordinal))
                throw new Exception($"Expected '{actual}' to contain '{expected}'.");
        }

        public static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new Exception($"Expected {typeof(TException).Name} to be thrown.");
        }
    }
}
