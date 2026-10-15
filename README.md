# CivicConnect - MVC + PostgreSQL + REST API + Observer + Adapter

This version extends the simple MVC project while keeping the code straightforward for a college-level project.

## What was added

- PostgreSQL persistence using Entity Framework Core and Npgsql.
- A repository interface and PostgreSQL repository implementation.
- Application interfaces such as `IRequestService`.
- A REST API under `/api/servicerequests` that returns JSON.
- Observer pattern for reacting to service-request changes.
- Adapter pattern for translating CivicConnect notifications to an external notification-client contract.
- A safe local `DemoNotificationClient` is used instead of storing a real external API key or password.

## Architecture

```text
Razor UI ------------------------┐
                                 │
REST API ------------------------┤
                                 ▼
                         IRequestService
                                 │
                                 ▼
                           RequestService
                                 │
                                 ├── IRequestSubject
                                 │      └── RequestNotificationObserver
                                 │                │
                                 │                ▼
                                 │        INotificationAdapter
                                 │                │
                                 │                ▼
                                 │        NotificationAdapter
                                 │                │
                                 │                ▼
                                 │       External notification client
                                 │
                                 ▼
                     IServiceRequestRepository
                                 │
                                 ▼
                    PostgreSQL / EF Core / Npgsql
```

## Why use a REST API when PostgreSQL is added?

PostgreSQL and REST solve different problems.

- PostgreSQL is the persistent data store.
- REST is an interface that allows clients to communicate with the application over HTTP.
- MVC Razor pages can continue using the same service layer without becoming a REST client.
- A mobile app, React frontend, another system, or an integration could use the REST API later.

The database does not automatically require a REST API. They are separate architectural decisions. In this version both are included so the project demonstrates a persistent backend and a reusable HTTP API.

## REST endpoints

- `GET /api/servicerequests` - return all service requests as JSON.
- `GET /api/servicerequests/{id}` - return one request.
- `POST /api/servicerequests` - create a request.
- `POST /api/servicerequests/{id}/assign/{staffId}` - assign a request.
- `POST /api/servicerequests/{id}/advance` - advance status.

## PostgreSQL setup

1. Install PostgreSQL 17 and create a new, empty database for CivicConnect.
2. Provision its schema by executing [`CivicConnectMvc/Database/DatabaseSchema.sql`](CivicConnectMvc/Database/DatabaseSchema.sql) against that newly created, empty database only. The script creates the seven-table public schema structure; it does not contain application users or seed data.
3. Configure your own development database connection using .NET User Secrets. Do not put credentials in `appsettings.json` or commit them to source control.
4. Ensure PostgreSQL is running and the secret points to the database you provisioned.
5. Run the application using the .NET CLI or Visual Studio. The application does not run `EnsureCreated()`, migrations, or automatic sample-data seeding; schema provisioning is a separate setup step.


No real database credentials or external notification API keys are included in this project.

## Observer pattern

`RequestService` implements `IRequestSubject`. It maintains a list of `IRequestObserver` objects and calls `Notify()` after a request is created, assigned, or its status is changed.

`RequestNotificationObserver` implements `IRequestObserver`. It reacts to those changes and sends a notification through `INotificationAdapter`.

Flow:

```text
RequestService
     │
     │ Notify()
     ▼
RequestNotificationObserver
     │
     ▼
INotificationAdapter
```

## Adapter pattern

`NotificationAdapter` implements `INotificationAdapter` and wraps an `IExternalNotificationClient`.

CivicConnect uses a simple notification model, while the external client exposes a different method (`SendMessage(recipient, title, body)`). The adapter translates between the two contracts.

Flow:

```text
CivicConnect NotificationMessage
              │
              ▼
      NotificationAdapter
              │
              ▼
 IExternalNotificationClient
              │
              ▼
    DemoNotificationClient
```

The demo client only writes to the console. A real vendor client can replace it later without changing the controller or domain logic.

## Run

Open `CivicConnectMvc/CivicConnectMvc.csproj` in Visual Studio with the .NET 10 SDK installed, make sure PostgreSQL is running, update the local connection string, and press F5.

## Test

Run:

`dotnet test CivicConnectMvc.Tests/CivicConnectMvc.Tests.csproj`

## Example REST request

`POST /api/servicerequests`

```json
{
  "title": "Broken street light",
  "description": "The light outside the library is not working.",
  "category": "Facility Fault"
}
```

The API returns the created service request as JSON and uses the same `IRequestService` used by the MVC controllers. This avoids duplicating business logic between the web UI and API.
## Verification

The `CivicConnectMvc.Tests` project is a small console-based verification runner. It does not require xUnit or the Visual Studio test runner.

From the solution folder:

```powershell
dotnet build
dotnet run --project .\CivicConnectMvc.Tests\CivicConnectMvc.Tests.csproj
```

The runner checks service-request status rules, staff assignment, the Adapter pattern, and the Observer pattern.

