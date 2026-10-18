# CivicConnect — Architecture Decision Register

**Project:** CivicConnect — SEN381  
**Document:** ADR Register v1.0  
**Date documented:** 18 October 2026  
**Milestone:** 4 — Final Project Evaluation  
**Status:** Retrospective documentation — pending team review  
**Release baseline:** `a2629c7c6a5300888d0df793089a3f2729459c95`

---

## ADR-001: Layered Modular Monolithic Architecture

**Status:** Accepted — implemented  
**Decision established:** Milestone 2  
**Decision documented retrospectively:** Milestone 4

### Context

CivicConnect required a maintainable architecture capable of supporting municipal service-request management within a three-person development team and a limited academic schedule.

### Alternatives Considered

1. Unstructured monolithic application
2. Microservices architecture
3. Layered modular monolith

### Decision

Adopt a layered modular monolithic architecture using ASP.NET Core MVC, separating presentation, application services, domain models and infrastructure responsibilities.

### Justification

This architecture supports separation of concerns, maintainability, testability and simpler deployment without introducing unnecessary distributed-system complexity.

### Consequences

**Positive:**
- Clearer separation of responsibilities.
- Supports independent testing of selected components.
- Simplifies deployment and development coordination.

**Negative:**
- Components share a single deployment unit.
- Architectural boundaries require continued developer discipline.

### Implementation Evidence

- `Controllers/` and `Views/` — presentation
- `Services/RequestService.cs` — application operations
- `Models/` — domain models
- `Infrastructure/Repositories/` — persistence implementation
- `Interfaces/` — component abstractions

---

## ADR-002: PostgreSQL and Entity Framework Core Persistence

**Status:** Accepted — implemented  
**Decision established:** Milestone 2  
**Decision documented retrospectively:** Milestone 4

### Context

CivicConnect required persistent storage for service requests, assignments, users, categories, status histories, request actions and resolutions.

Maintaining relational consistency was important because request-management operations affect interconnected records.

### Alternatives Considered

1. In-memory storage
2. Document-oriented database
3. PostgreSQL relational database

### Decision

Use PostgreSQL as the relational database with Entity Framework Core and Npgsql for application persistence.

Apply the Repository pattern to separate database-access implementation from application services.

### Justification

PostgreSQL supports relational integrity and transactional operations. Entity Framework Core enables object-relational mapping, while repository abstractions improve separation of responsibilities.

### Consequences

**Positive:**
- Persistent relational data storage.
- Support for database constraints and transactions.
- Improved separation between application logic and persistence.
- Supports integrity, rollback and concurrency verification.

**Negative:**
- Requires database configuration and maintenance.
- Schema changes require migration management.
- Database compatibility and concurrency issues must be tested.

### Implementation Evidence

- `Data/CivicConnectDbContext.cs`
- `Infrastructure/Repositories/ServiceRequestRepository.cs`
- `Interfaces/IServiceRequestRepository.cs`
- `Program.cs` — `UseNpgsql(connectionString)`
- Historical PostgreSQL integration tests recorded during Milestone 3

---

## ADR-003: Observer and Adapter Notification Architecture

**Status:** Accepted — implemented with simulated delivery  
**Decision established:** Milestone 2  
**Decision documented retrospectively:** Milestone 4

### Context

CivicConnect required a mechanism for reacting to service-request changes and providing notification feedback without tightly coupling application logic to a specific external notification provider.

### Alternatives Considered

1. Direct notification calls inside business logic
2. Provider-specific notification integration
3. Observer and Adapter design patterns

### Decision

Use the Observer pattern to respond to service-request changes and the Adapter pattern to isolate notification delivery behind interfaces.

### Justification

The Observer pattern separates request operations from notification handling.

The Adapter pattern allows notification integration to be changed without requiring significant modifications to application-service logic.

### Consequences

**Positive:**
- Reduced coupling between request management and notification infrastructure.
- Notification handling can be extended or replaced.
- Supports interface-based testing.

**Negative:**
- Additional interfaces and implementation classes increase structural complexity.
- Notifications execute synchronously in the current implementation.
- External delivery remains unverified.

### Implementation Evidence

- `Services/RequestService.cs`
- `Infrastructure/Notifications/RequestNotificationObserver.cs`
- `Infrastructure/Notifications/NotificationAdapter.cs`
- `Infrastructure/Notifications/DemoNotificationClient.cs`
- Notification dependency registrations in `Program.cs`

**Limitation:** The demonstration client writes notification messages to the console. Actual email or SMS delivery has not been established.

---

## ADR Register Summary

| ADR ID | Decision | Implementation Status |
|---|---|---|
| ADR-001 | Layered Modular Monolith | Implemented |
| ADR-002 | PostgreSQL + Entity Framework Core | Implemented |
| ADR-003 | Observer + Adapter Notifications | Implemented; external delivery unverified |

## Final Review and Approval

The three decisions are consistent with the architecture described in PED v4.0 and the source components reviewed during Milestone 4.

The register was consolidated retrospectively and requires final team review to confirm the historical decision rationale and alternatives.

**Production readiness:** These decisions do not establish production approval. The final CivicConnect production release recommendation remains NO-GO.

**Review status:** Pending team confirmation.
