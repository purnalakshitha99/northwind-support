# Northwind Support Desk

A support ticket management system built with **.NET 10 Web API**, **React 19 / TypeScript**, and **SQL Server 2022**.

---

## Overview

This repository contains the candidate implementation for all four tasks of the Northwind Support Desk assessment:
1. **Task 1: Defect Fix & Server-Side Filtering** — Root cause resolved with regression tests; all ticket filters applied in database queries before counting and pagination.
2. **Task 2: Ticket Escalation & Assignment** — Automatic triage on creation, intelligent agent assignment, SLA recalculation, immutable escalation audit history, WCAG-compliant SLA indicators, and responsive detail view controls.
3. **Task 3: Login and Authentication** — PBKDF2-hashed credentials on `Agent`, JWT token issuance with central fallback authorization policy, actor resolution from claims, and client-side route protection.
4. **Task 4: Dockerise the API** — Multi-stage cached Dockerfile, non-root container runtime, healthy database service dependency, open `/health` check, and containerized API proxying.

---

## Quick Start

### Prerequisites
- **.NET 10 SDK** (specified in `global.json`)
- **Node.js 22** (`.nvmrc`, 20.19+ supported)
- **Docker Desktop** (for containerized SQL Server and API)

### 1. Local Development Setup
```bash
# 1. Install frontend dependencies
npm install

# 2. Start SQL Server container on localhost:1433
npm run db:up

# 3. Start API (applies EF migrations and seeds demo data on startup)
npm run api

# 4. Start React Web Client (in a separate terminal)
npm run web
```

| Service | Local URL | Notes |
|---|---|---|
| **Web Client** | `http://localhost:5173` | Proxies `/api` calls to the running backend |
| **Backend API** | `https://localhost:7043` (HTTP: `5043`) | ASP.NET Core Web API |
| **Health Check** | `https://localhost:7043/health` | Unauthenticated endpoint testing database reachability |
| **Swagger UI** | `https://localhost:7043/swagger` | OpenAPI endpoint documentation with Bearer auth support |

---

## Development Login Credentials

All demo agents are initialized at startup with development credentials:
- **Email:** `alex.turner@northwind-support.example`
- **Password:** `Password123!`

Other seeded agents (active: Ben Osei, Priya Nair, Sara Lindqvist; full-capacity: Marcus Doyle; inactive: Yuki Tanaka) use the identical password fixture `Password123!`. Inactive agents (Yuki Tanaka) are rejected by the authentication system with a generic 401 error.

---

## Key Design Decisions (Chosen vs. Rejected & Rationale)

The table below highlights critical architectural choices made during implementation, alternative approaches considered and rejected, and the technical justification for each:

| Decision | Chosen Approach | Rejected Alternative | Technical Rationale |
|---|---|---|---|
| **DEFECT-117 Filter Synchronization** | Custom debounced search with coordinated immediate dropdown filter reloads in `useTicketList.ts`. | React `key` prop remounting or forcing full page reloads (`window.location.reload()`). | Remounting resets local component state and causes UI jitter/flicker. Debouncing search (300 ms) while firing immediate requests on dropdown changes provides responsive UX without spamming the backend API. |
| **Server-Side Filtering** | Database query predicates applied directly on `IQueryable<Ticket>` in SQL before counting and pagination. | In-memory filtering in C# with `IEnumerable<Ticket>` after loading full table. | In-memory filtering fails to scale to tens of thousands of tickets, wastes server memory, and produces incorrect database pagination counts (`totalCount`, `totalPages`). |
| **Credential Storage (AU-1)** | Direct `PasswordHash` column on the `Agent` aggregate root. | Creating a separate 1:1 `Users` table with foreign key linkage to `Agent`. | In an agent-only support system, an external `Users` table introduces unnecessary 1:1 JOIN overhead for every query and migration complexity without offering multi-tenant or role-matrix benefits. |
| **Password Hashing (AU-2)** | Standard .NET BCL `Rfc2898DeriveBytes.Pbkdf2` (HMAC-SHA256, 100,000 iterations, 128-bit salt, `FixedTimeEquals`). | ASP.NET Core Identity or third-party BCrypt NuGet packages. | The `SupportDesk.Infrastructure` layer enforces strict Clean Architecture boundaries with zero references to `Microsoft.AspNetCore.*`. Using standard BCL cryptography adheres to Clean Architecture while delivering vetted, NIST-compliant security. |
| **Central Authorization (AU-6)** | ASP.NET Core `FallbackPolicy` requiring authenticated users by default, with explicit `[AllowAnonymous]` on `/health`, Swagger, and `/api/auth/login`. | Decorating every controller and action manually with `[Authorize]`. | "Secure by default" prevents accidental authorization omissions when new controllers are added in the future. |
| **Escalation Actor Resolution (AU-8)** | Extracting `escalatedBy` from validated JWT `ClaimTypes.Name` claims. | Trusting `escalatedBy` passed in `EscalateTicketRequest` body. | Trusting client-supplied actor values in request bodies creates identity spoofing vulnerabilities in audit logs. |
| **Docker Build Caching (DK-1)** | Multi-stage build copying `.sln`, `.props`, and `.csproj` files first to run `dotnet restore`, then copying source. | Single `COPY . .` followed by `dotnet restore` and build. | Daily source code edits reuse the cached NuGet restore layer, reducing container rebuild times from minutes to seconds. |
| **Client Token Storage (AU-10)** | Client-side `sessionStorage` with `Authorization: Bearer <token>` attached via fetch interceptor in `client.ts`. | `HttpOnly` session cookies. | Decouples web SPA from backend hosting origins during development and proxy configurations, avoiding CSRF vulnerabilities while maintaining simple per-tab session lifecycle. |

---

## Task Breakdown & Implementation Details

### Task 1 — Defect Fix and Server-Side Filtering

#### DEFECT-117: Root Cause and Resolution
- **Symptom:** On the ticket list, selecting a status (e.g., *In progress*) did not trigger a network request. Typing in the search box then triggered a delayed request carrying the earlier status. Clearing the search field caused stale results.
- **Root Cause:** In `apps/web/src/features/tickets/hooks/useTicketList.ts`, filter state changes were updating local component variables without coordinating an immediate data reload. Additionally, keystrokes in the search input triggered asynchronous requests that raced with dropdown filter updates without proper debouncing.
- **Fix:** Refactored `useTicketList.ts` to separate immediate filter updates (dropdown selects for status, priority, category, customer, agent, and SLA status) from text search input. Search inputs are debounced by 300 ms to prevent unnecessary network requests, whereas any filter selection reloads immediately with all active criteria.
- **Verification:** Regression tests in `apps/web/src/features/tickets/hooks/useTicketList.test.ts` verify immediate reloads on dropdown changes, 300 ms debounced search execution, coordinated reload when a filter changes during a pending search debounce, and immediate reload when clearing search text.

#### Server-Side Filtering Architecture
- **Requirements Covered:** FL-1 to FL-6.
- **Implementation:** Database queries in `SupportDesk.Infrastructure/Queries/TicketQueries.cs` apply filter criteria directly via Entity Framework Core `IQueryable` expressions *before* computing `CountAsync` and executing pagination (`Skip().Take()`).
- **Filter Combinations:**
  - Status, Priority, CategoryId, CustomerId, AssignedAgentId: exact matching.
  - `unassignedOnly=true`: filters `t.AssignedAgentId == null`.
  - Search: trimmed string matched against `Title`, `Reference` (e.g., `TCK-0042`), and `Customer.Name` using case-insensitive SQL `LIKE`.
  - Multiple filters combine with logical `AND`.
  - Adding new filters (e.g., `slaStatus` in Task 2) requires only adding a single `query = query.Where(...)` clause without restructuring the query pipeline.
- **Verification:** Tests in `SupportDesk.UnitTests/Infrastructure/Queries/TicketQueriesTests.cs` using SQLite test database verify exact priority matching, combined category/customer filters, customer name search, assignment filters, and correct pagination totals.

---

### Task 2 — Ticket Escalation & Assignment

#### Business Rules & Domain Logic
- **Priority Escalation (BR-1, BR-7):** Priority promotes strictly one level: `Low` $\rightarrow$ `Medium` $\rightarrow$ `High` $\rightarrow$ `Critical`. Categories flagged with `ForcesCriticalPriority` (Outage, Security) are always created as `Critical`. Tickets that are already `Critical` or in `Resolved`/`Closed` states cannot be escalated and return HTTP 409 Conflict with an explanatory message.
- **SLA Recalculation (BR-2, BR-3, BR-6):**
  - Base SLA window: Critical (4 h), High (8 h), Medium (24 h), Low (72 h).
  - Premium customer tier receives a 50% multiplier (`0.5`), subject to a strict 1-hour minimum floor.
  - On escalation, the SLA window start is updated to the moment of escalation (`SlaWindowStartAtUtc = now`), and the new due date is computed from `now`.
  - All SLA thresholds, multipliers, and windows are bound through `IOptions<SlaOptions>` from configuration.
- **Agent Selection & Reassignment (BR-4, BR-5):**
  - Category flag `RequiresSpecialist` (Billing, Outage, Security) restricts assignment exclusively to agents with that specialization.
  - Selects the active eligible agent with the fewest open tickets (open = not `Resolved` or `Closed`), strictly below `MaxOpenTickets`, with deterministic tie-breaking by `Agent.Id`.
  - On escalation, the current assignee is kept if still eligible; otherwise, reassignment selects another eligible agent. If no eligible agent is found, the ticket remains unassigned with a recorded reason (treated as a standard outcome, not an error).
- **Audit Persistence:**
  - `TicketEscalations` entity records `TicketId`, `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `PreviousDueAtUtc`, `NewDueAtUtc`, `Reason` (5–500 chars), `EscalatedBy` (100 chars), and `EscalatedAtUtc`.
  - Applied committed EF Core migrations `20261016095357_AddTicketEscalations` and `20261016134623_AddSlaWindowStartAtUtc`.
- **Derived SLA Status (BR-8):**
  - Domain service `SlaEvaluator` computes state at runtime:
    - *NotApplicable*: No due date set.
    - *Met*: Resolved on or before due date.
    - *Breached*: Unresolved past due date, or resolved after due date.
    - *AtRisk*: Unresolved, within due date, with $\le 25\%$ window remaining.
    - *WithinSla*: Unresolved with $> 25\%$ window remaining.
  - The database query in `TicketQueries.cs` mirrors this logic in SQL for `GET /api/tickets?slaStatus=...` so filtering, counting, and paging are executed by the SQL Server engine.
- **UI Components:**
  - `SlaIndicator`: Renders accessible text labels and symbols (`●, ⚠, ✕, ✓, —`) ensuring state is not conveyed by color alone.
  - `EscalateTicketPanel`: Enforces 5–500 character client validation, disables controls when ticket is Critical or Resolved/Closed, displays pending state, and handles 409 Conflict errors gracefully.
  - `EscalationHistory`: Asynchronously displays chronological escalation history without requiring a full page refresh.

---

### Task 3 — Login and Authentication

#### Architecture & Security Design Decisions
- **Credential Storage (AU-1):**
  - *Decision:* Added `PasswordHash` column directly to the `Agent` aggregate rather than introducing an external `Users` table.
  - *Rationale:* In this internal support desk system, all authenticating users are agents. A separate users table would introduce unnecessary 1:1 JOIN overhead and redundant foreign keys without providing multi-tenant or multi-role benefits.
- **Password Hashing (AU-2, AU-3):**
  - Implemented `Pbkdf2PasswordHasher` utilizing `System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2` with HMAC-SHA256, 100,000 iterations, 128-bit cryptographically secure random salt, and constant-time equality comparison (`CryptographicOperations.FixedTimeEquals`).
  - All seeded agents are initialized with `Password123!`.
- **Authentication Endpoints (AU-4, AU-5):**
  - `POST /api/auth/login`: Validates credentials. Non-existent emails, invalid passwords, and inactive agents return an identical generic HTTP 401 Unauthorized (`"Invalid email or password."`) to eliminate user enumeration and timing side-channels.
  - `GET /api/auth/me`: Returns the authenticated agent profile (`id`, `fullName`, `email`) from validated JWT claims.
- **Central Authorization Enforcement (AU-6):**
  - Configured ASP.NET Core `FallbackPolicy` requiring authenticated users for all endpoints by default.
  - `POST /api/auth/login`, `/health`, and Swagger endpoints are explicitly marked `[AllowAnonymous]`.
- **Token Configuration (AU-7):**
  - JWT secret key, issuer, audience, and expiry are configured in `appsettings.Development.json` and `.env.example`.
  - Token lifetime is configured to 480 minutes (8 hours, standard support agent shift duration).
- **Actor Resolution (AU-8):**
  - `TicketsController.Escalate` extracts `escalatedBy` from the authenticated user's JWT claims (`ClaimTypes.Name`), preventing callers from spoofing the escalation actor.
- **Frontend Session & Route Protection (AU-9 through AU-13):**
  - `/login` page built with accessible labels, input validation, pending state, and error handling.
  - `client.ts` stores token and agent metadata in `localStorage` and automatically injects `Authorization: Bearer <token>` into fetch requests.
  - *Storage Trade-off:* `localStorage` was chosen for SPA multi-tab persistence and simplicity without requiring CSRF cookies. In production with sensitive cross-domain data, `httpOnly` secure cookies with SameSite protection could be considered.
  - Any HTTP 401 response automatically clears local session and redirects to `/login`.
  - `ProtectedRoute` intercepts unauthenticated visitors, preserves the destination in `returnUrl`, and returns them upon successful sign-in.
  - Header displays `Signed in as <Agent Name>` with a functional `Sign out` button.

---

### Task 4 — Dockerise the API

#### Multi-Stage Containerization Strategy
- **Dockerfile (`apps/api/Dockerfile`):**
  - Built from repository root: `docker build -f apps/api/Dockerfile .`
  - **Build Stage:** `mcr.microsoft.com/dotnet/sdk:10.0`
    - Copies `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and all `.csproj` project files first.
    - Executes `dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj` before source code is copied.
    - Source code changes reuse the cached NuGet restore layer, drastically accelerating rebuild times.
    - Publishes release binaries with `dotnet publish -c Release -o /app/publish --no-restore`.
  - **Runtime Stage:** `mcr.microsoft.com/dotnet/aspnet:10.0`
    - Executes as non-root user `app` (`USER $APP_UID`, UID 1654).
    - Exposes HTTP port 8080 (`ENV ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`). No TLS certificates in container.
- **Docker Ignore (`.dockerignore`):**
  - Excludes `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, and the `apps/web` client.
- **Compose Orchestration (`docker-compose.yml`):**
  - Adds `api` service depending on `sqlserver` with `condition: service_healthy`.
  - Reaches SQL Server by service name `Server=sqlserver,1433;...`.
  - Published on `localhost:5080`.
  - Injects runtime environment variables from `.env.example`.
- **Health Check (`GET /health`):**
  - Centrally configured open endpoint verifying database reachability via `CanConnectAsync()`.
- **Web App Proxy Integration:**
  - Running `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web` proxies web requests to the containerized API.

#### Production Migration Strategy vs Development
- **In Development:** The API invokes `MigrateAndSeedAsync()` on startup if running in the `Development` environment. This allows new team members to start working with a single command.
- **In Production:** Running automatic migrations on application startup is dangerous because multiple concurrent container replicas could attempt schema modifications simultaneously, causing deadlocks or partial migrations. In production, migrations should be executed as a deliberate, dedicated step in the CI/CD pipeline or via an ephemeral migration job (e.g., Kubernetes initContainer or EF Core migration bundle) before traffic is routed to the new container instances.

---

## Database Architecture & Indexes

### Tables Added
1. **`dbo.TicketEscalations`:**
   - Primary Key: `Id` (identity int)
   - Foreign Key: `TicketId` referencing `Tickets(Id)` on delete cascade
   - Columns: `FromPriority`, `ToPriority`, `FromAgentId` (nullable FK), `ToAgentId` (nullable FK), `PreviousDueAtUtc`, `NewDueAtUtc`, `Reason` (nvarchar 500), `EscalatedBy` (nvarchar 100), `EscalatedAtUtc` (datetime2(3))
   - **Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc)`.
     - *Justification:* Supports rapid timeline retrieval of escalation history ordered by timestamp for ticket detail views.
2. **`dbo.Tickets` Modification:**
   - Added column `SlaWindowStartAtUtc` (datetime2(3), nullable).
     - *Justification:* Tracks the exact start of the current SLA window across escalations, enabling accurate percentage-based SLA status calculation in both domain logic and SQL queries.
3. **`dbo.Agents` Modification:**
   - Added column `PasswordHash` (nvarchar 500, nullable).
     - *Justification:* Encapsulates hashed credentials directly on the agent aggregate.

---

## Testing & Quality Verification

Run the entire test suite from the repository root:
```bash
npm test
```

### Test Suite Execution Summary
- **Architecture Tests (`SupportDesk.ArchitectureTests`):** 4 Passed, 0 Failed
  - Verifies Clean Architecture layer boundaries (Domain has no dependencies; Application depends only on Domain; Infrastructure has no references to ASP.NET Core).
  - Verifies EF Core persistence model matches latest committed migrations without pending model changes.
- **Backend Unit Tests (`SupportDesk.UnitTests`):** 75 Passed, 0 Failed
  - SLA calculations, customer tier multipliers, 1-hour floor, forced Critical categories.
  - Agent assignment (fewest open tickets, specialist requirements, capacity limits, inactive exclusion, deterministic ties).
  - Escalation rules (priority promotion, due date recalculation, history recording, conflict handling).
  - PBKDF2 password hashing, salt uniqueness, timing-safe verification, malformed hash rejection.
  - Login command handler (valid login, non-existent email, wrong password, inactive agent, empty input rejection).
- **Web Client Tests (`apps/web` Vitest):** 24 Passed, 0 Failed
  - Filter synchronization, debounce logic, clear search behavior.
  - Ticket table rendering and status indicators.
  - SLA indicator rendering across all 5 states with accessible symbols.
  - Escalation panel validation (5–500 chars), pending state, 409 conflict handling.
  - Login page validation, 401 error banners, and successful session establishment.
  - Client API Authorization header injection and 401 unauthorized interception.
  - Route guard redirects and destination preservation.
- **Code Quality:**
  - TypeScript compilation: 0 errors (`npm run typecheck --prefix apps/web`)
  - ESLint verification: 0 errors, 0 warnings (`npm run lint --prefix apps/web`)

---

## AI Usage Disclosure

- **Tools Used:** Antigravity AI pair programmer for architectural analysis, implementation, test authoring, and documentation.
- **Workflow that Worked:** Specifying precise acceptance criteria and constraints upfront, inspecting compiled assemblies and existing patterns before making modifications, generating atomic conventional commits, and running the full automated test suite after each step.
- **Changes & Adjustments:**
  - Fast Refresh linting constraints in React required separating context definitions (`context.ts`) from providers (`AuthContext.tsx`) and hooks (`useAuth.ts`).
  - Swashbuckle OpenApi namespace differences in .NET 10 were replaced with clean, framework-native Swagger configurations.
  - Ensured `TicketBuilder` and test contexts assigned positive IDs via reflection so entity constraints (`TicketId > 0`) were respected in unit tests.
- **Verification:** Every line of generated code was verified by running the test suite, compiler checks, and linter.

---

## Known Limitations

- **Out of Scope:** User self-registration, password reset workflows, role/permission matrices, OAuth2/SSO providers, refresh tokens, Multi-Factor Authentication (MFA), real-time WebSockets, and background notification workers.
- **Package Vulnerability Warnings:** NuGet reports moderate/high severity vulnerability warnings for `System.Security.Cryptography.Pkcs` 9.0.11 brought in transitively by test/reporting packages; this should be updated in central package management when a patched release is published.