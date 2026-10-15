# PostgreSQL Integration Test Results

- **Date:** 15 October 2026
- **Database:** `civicconnect_m3_test` (isolated test database)
- **Integration result:** 9/9 passed

| Integration test | Result |
| --- | --- |
| `Request_Creation_And_Retrieval_Persist` | PASS |
| `Unsupported_Persisted_Status_Is_Read_And_Not_Actionable` | PASS |
| `Assignable_Staff_Query_Uses_Persisted_Roles` | PASS |
| `Invalid_Request_Foreign_Keys_Are_Rejected` | PASS |
| `Assignment_Is_Persisted` | PASS |
| `Status_And_History_Save_Atomically` | PASS |
| `Transaction_Rollback_Removes_Test_Data` | PASS |
| `Optimistic_Concurrency_Rejects_Stale_Write` | PASS |
| `Resolution_Is_Persisted` | PASS |

- **Offline tests:** 10/10 passed.
- **Build:** succeeded with no warnings or errors.
- **Isolation:** test cases use transaction rollback to isolate test data.
- **Runner:** custom console test runner; this is not an xUnit/NUnit test-SDK suite.

Commands:

```powershell
dotnet run --project CivicConnectMvc.Tests/CivicConnectMvc.Tests.csproj -- --postgres
dotnet run --project CivicConnectMvc.Tests/CivicConnectMvc.Tests.csproj
dotnet build CivicConnectMvc.sln
```
