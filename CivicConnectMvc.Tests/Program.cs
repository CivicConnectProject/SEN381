namespace CivicConnectMvc.Tests;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--postgres")
            return PostgreSqlPersistenceTests.RunAll();

        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: CivicConnectMvc.Tests [--postgres]");
            return 2;
        }

        var tests = new (string Name, Action Test)[]
        {
            (nameof(ApiHttpTests.Invalid_Create_Returns_ValidationProblem), ApiHttpTests.Invalid_Create_Returns_ValidationProblem),
            (nameof(ServiceRequestTests.Ef_Model_Maps_M2_PostgreSql_Schema), ServiceRequestTests.Ef_Model_Maps_M2_PostgreSql_Schema),
            (nameof(ServiceRequestTests.New_Request_Starts_As_Submitted), ServiceRequestTests.New_Request_Starts_As_Submitted),
            (nameof(ServiceRequestTests.Valid_Status_Transition_Is_Allowed), ServiceRequestTests.Valid_Status_Transition_Is_Allowed),
            (nameof(ServiceRequestTests.Status_Advance_Is_Offered_Only_When_Allowed), ServiceRequestTests.Status_Advance_Is_Offered_Only_When_Allowed),
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

        return passed == tests.Length ? 0 : 1;
    }
}
