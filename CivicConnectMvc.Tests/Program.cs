namespace CivicConnectMvc.Tests;

public class Program
{
    public static void Main()
    {
        var tests = new (string Name, Action Test)[]
        {
            (nameof(ServiceRequestTests.New_Request_Starts_As_Submitted), ServiceRequestTests.New_Request_Starts_As_Submitted),
            (nameof(ServiceRequestTests.Valid_Status_Transition_Is_Allowed), ServiceRequestTests.Valid_Status_Transition_Is_Allowed),
            (nameof(ServiceRequestTests.Invalid_Status_Transition_Is_Rejected), ServiceRequestTests.Invalid_Status_Transition_Is_Rejected),
            (nameof(ServiceRequestTests.Active_Staff_Can_Be_Assigned), ServiceRequestTests.Active_Staff_Can_Be_Assigned),
            (nameof(ServiceRequestTests.Requester_Cannot_Be_Assigned), ServiceRequestTests.Requester_Cannot_Be_Assigned),
            (nameof(ServiceRequestTests.Closed_Request_Cannot_Be_Reassigned), ServiceRequestTests.Closed_Request_Cannot_Be_Reassigned),
            (nameof(ServiceRequestTests.Adapter_Translates_Notification_To_Vendor_Client), ServiceRequestTests.Adapter_Translates_Notification_To_Vendor_Client),
            (nameof(ServiceRequestTests.Observer_Receives_Request_Change), ServiceRequestTests.Observer_Receives_Request_Change)
        };

        var passed = 0;

        Console.WriteLine("CivicConnect verification tests");
        Console.WriteLine("-------------------------------");

        foreach (var test in tests)
        {
            try
            {
                test.Test();
                Console.WriteLine($"PASS: {test.Name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: {test.Name}");
                Console.WriteLine($"      {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed}/{tests.Length} tests passed.");

        if (passed != tests.Length)
            Environment.ExitCode = 1;
    }
}
