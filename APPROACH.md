# Implementation Approach — Northwind Support Desk

This document details the architectural analysis, design decisions, implementation strategy, and verification results across all four tasks of the Northwind Support Desk candidate assignment.

---

## Key Design Decisions (Chosen vs. Rejected & Rationale)

| Decision Area | Chosen Approach | Rejected Alternative | Technical Rationale |
|---|---|---|---|
| **DEFECT-117 Filter Synchronization** | Custom debounced search (300 ms) with coordinated immediate dropdown filter reloads in `useTicketList.ts`. | React `key` prop remounting or forcing full page reloads (`window.location.reload()`). | Remounting resets local component state and causes UI jitter/flicker. Debouncing search while firing immediate requests on dropdown changes provides responsive UX without spamming the backend API. |
| **Server-Side Filtering** | Database query predicates applied directly on `IQueryable<Ticket>` in SQL before counting and pagination. | In-memory filtering in C# with `IEnumerable<Ticket>` after loading full table. | In-memory filtering fails to scale to tens of thousands of tickets, wastes server memory, and produces incorrect database pagination counts (`totalCount`, `totalPages`). |
| **Credential Storage (AU-1)** | Direct `PasswordHash` column on the `Agent` aggregate root. | Creating a separate 1:1 `Users` table with foreign key linkage to `Agent`. | In an agent-only support system, an external `Users` table introduces unnecessary 1:1 JOIN overhead for every query and migration complexity without offering multi-tenant or role-matrix benefits. |
| **Password Hashing (AU-2)** | Standard .NET BCL `Rfc2898DeriveBytes.Pbkdf2` (HMAC-SHA256, 100,000 iterations, 128-bit salt, `FixedTimeEquals`). | ASP.NET Core Identity or third-party BCrypt NuGet packages. | The `SupportDesk.Infrastructure` layer enforces strict Clean Architecture boundaries with zero references to `Microsoft.AspNetCore.*`. Using standard BCL cryptography adheres to Clean Architecture while delivering vetted, NIST-compliant security. |
| **Central Authorization (AU-6)** | ASP.NET Core `FallbackPolicy` requiring authenticated users by default, with explicit `[AllowAnonymous]` on `/health`, Swagger, and `/api/auth/login`. | Decorating every controller and action manually with `[Authorize]`. | "Secure by default" prevents accidental authorization omissions when new controllers are added in the future. |
| **Escalation Actor Resolution (AU-8)** | Extracting `escalatedBy` from validated JWT `ClaimTypes.Name` claims. | Trusting `escalatedBy` passed in `EscalateTicketRequest` body. | Trusting client-supplied actor values in request bodies creates identity spoofing vulnerabilities in audit logs. |
| **Docker Build Caching (DK-1)** | Multi-stage build copying `.sln`, `.props`, and `.csproj` files first to run `dotnet restore`, then copying source. | Single `COPY . .` followed by `dotnet restore` and build. | Daily source code edits reuse the cached NuGet restore layer, reducing container rebuild times from minutes to seconds. |
| **Client Token Storage (AU-10)** | Client-side `sessionStorage` with `Authorization: Bearer <token>` attached via fetch interceptor in `client.ts`. | `HttpOnly` session cookies. | Decouples web SPA from backend hosting origins during development and proxy configurations, avoiding CSRF vulnerabilities while maintaining simple per-tab session lifecycle. |

---

## Task 1 — Defect Fix and Server-Side Filtering

### DEFECT-117: Filters not taking effect / stale requests
- **Symptom:** On the ticket list, selecting a status (e.g., *In progress*) did not trigger a network request. Typing in the search input triggered a delayed request carrying the previously selected status. Clearing the search caused stale results.
- **Root Cause:** In `useTicketList.ts`, filter changes were updating local component state but did not trigger immediate data reloads. Additionally, keystroke events in the search input lacked proper debouncing, causing race conditions between asynchronous search requests and dropdown filter updates.
- **Fix:** Refactored `useTicketList.ts` to separate immediate filter changes (status, priority, category, customer, agent, SLA status) from debounced text search (300 ms debounce timer). Changing any dropdown filter immediately issues a new `GET /api/tickets` request with all active filters.
- **Regression Tests:** Vitest tests in `useTicketList.test.ts` verify:
  1. Immediate reload upon dropdown filter selection.
  2. Debouncing of search inputs to avoid redundant requests per keystroke.
  3. Coordinated reloads when changing filters while a search debounce is pending.
  4. Clearing the search field resets the search parameter immediately.

### Server-Side Filtering
- **Requirements Covered:** FL-1 through FL-6.
- **Implementation:**
  - Extended `TicketQuery` in `SupportDesk.Application` with filter fields.
  - Implemented database-level filtering in `TicketQueries.cs` using Entity Framework Core IQueryable expression trees before `CountAsync` and pagination (`Skip().Take()`).
  - Search matches trimmed ticket title, reference (`TCK-xxxx`), or customer name using SQL `LIKE`.
  - Filters combine using logical `AND`.
  - `unassignedOnly=true` filters where `AssignedAgentId == null`.
  - Adding subsequent filters (such as Task 2's `slaStatus`) extends the query pipeline with a single predicate without altering query structure.
- **Verification:** Unit tests in `TicketQueriesTests.cs` using SQLite test database verify exact priority matching, combined category/customer filters, customer name search, assignment filters, and correct pagination totals.

---

## Task 2 — Ticket Escalation & Assignment

### Business Rules & Architecture Placement
- **Priority Escalation (BR-1, BR-7):** Priority advances strictly one level (`Low` $\rightarrow$ `Medium` $\rightarrow$ `High` $\rightarrow$ `Critical`). Tickets already `Critical` or in `Resolved`/`Closed` states cannot be escalated and reject with HTTP 409 Conflict. Implemented within the `Ticket` aggregate root (`ticket.EscalatePriority(now)`) to uphold domain invariants.
- **SLA Recalculation (BR-2, BR-3, BR-6):**
  - New due date formula: $\text{Now} + \max(1.0, \text{BaseHours} \times \text{CustomerTierMultiplier})$.
  - Window start is reset to the escalation timestamp (`ticket.SetDueAt(newDueAt, escalatedAt)`), storing `SlaWindowStartAtUtc` on the ticket.
  - Base SLA hours, multipliers, and thresholds are bound via `IOptions<SlaOptions>` from configuration, avoiding hard-coded values.
- **Intelligent Agent Reassignment (BR-4, BR-5):**
  - If category requires a specialist (`requiresSpecialist = true`), only agents with a matching specialization are eligible.
  - Current assignee is retained if still eligible; otherwise, reassignment selects the active eligible agent with the fewest open tickets (open = not `Resolved`/`Closed`), tie-broken deterministically by `Agent.Id`.
  - If no eligible agent is available, the ticket remains unassigned with a recorded triage reason.
- **Audit Persistence:**
  - Dedicated `TicketEscalations` entity mapping to `dbo.TicketEscalations` table via EF Core migration.
  - Stores `TicketId`, `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `PreviousDueAtUtc`, `NewDueAtUtc`, `Reason` (5–500 chars), `EscalatedBy`, and `EscalatedAtUtc`.
  - Composite index `IX_TicketEscalations_TicketId_EscalatedAtUtc` created to accelerate timeline queries.
- **Derived SLA Status (BR-8):**
  - Domain service `SlaEvaluator` calculates SLA state:
    - *NotApplicable*: No due date set.
    - *Met*: Resolved on or before due date.
    - *Breached*: Unresolved and past due date, or resolved late.
    - *AtRisk*: Unresolved, within due date, with $\le 25\%$ of window remaining.
    - *WithinSla*: Unresolved with $> 25\%$ of window remaining.
  - Database queries project this logic directly in SQL for `GET /api/tickets?slaStatus=...` so filtering, sorting, and pagination happen entirely in the database engine.
- **Frontend Components:**
  - Reusable `SlaIndicator` renders text labels and distinct symbols (`●, ⚠, ✕, ✓, —`) to comply with WCAG accessibility standards without relying on color alone.
  - `EscalateTicketPanel` enforces 5–500 character client validation, shows loading spinners, handles 409 conflict errors, and refreshes ticket state in-place.
  - `EscalationHistory` displays chronological before/after transition records.
  - `CreateTicketPage` displays post-creation triage outcome feedback.

---

## Task 3 — Login and Authentication

### Authentication Architecture & Security
- **Credential Storage (AU-1):**
  - Chose adding `PasswordHash` column to the `Agent` aggregate.
  - *Rationale:* In this internal support desk system, only support agents authenticate. Introducing a separate `Users` table would introduce unnecessary 1:1 JOIN overhead and redundant keys without any multi-tenant or multi-role benefits.
  - Database column created via committed EF Core migration `AddAgentPasswordHash`.
- **Cryptographic Hashing (AU-2, AU-3):**
  - Implemented `Pbkdf2PasswordHasher` using .NET's standardized `Rfc2898DeriveBytes.Pbkdf2` with HMAC-SHA256, 100,000 iterations, 128-bit cryptographically secure random salt, and constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
  - All seeded agents are initialized with development credentials: `Password123!`.
- **Authentication Endpoints (AU-4, AU-5):**
  - `POST /api/auth/login`: Accepts email and password. To prevent user enumeration attacks and timing side-channels, non-existent emails, invalid passwords, and inactive agents return an identical generic HTTP 401 Unauthorized response (`"Invalid email or password."`).
  - `GET /api/auth/me`: Returns the currently authenticated agent's profile (`id`, `fullName`, `email`) from claims.
- **Central Authorization Policy (AU-6):**
  - Configured ASP.NET Core `FallbackPolicy` requiring authenticated users across all endpoints by default (`options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`).
  - `POST /api/auth/login`, `/health`, and Swagger endpoints are explicitly marked `[AllowAnonymous]`.
- **Actor Resolution (AU-8):**
  - `TicketsController.Escalate` extracts `escalatedBy` from the JWT `ClaimTypes.Name` claim rather than accepting caller input from the request body.
- **Token Configuration (AU-7):**
  - JWT secret key, issuer, audience, and expiration are loaded from configuration (`Authentication:Jwt`) and documented in `.env.example`.
  - Token lifetime is set to 480 minutes (8 hours, standard support shift).
- **Frontend Session Management (AU-9, AU-10, AU-11, AU-12, AU-13):**
  - Accessible `/login` page using canonical `Field` and `ErrorMessage` components with full client validation.
  - Client API (`client.ts`) stores token and agent metadata in `localStorage` and automatically attaches `Authorization: Bearer <token>` to all HTTP requests.
  - *Storage Trade-off:* `localStorage` was chosen for seamless SPA page reloads and multi-tab state. While `httpOnly` cookies mitigate XSS risks, they introduce CSRF attack vectors and domain coupling. For this token-based architecture, `localStorage` provides clean decoupling.
  - Any HTTP 401 response automatically clears local session and redirects to `/login`.
  - `ProtectedRoute` component guards protected routes, capturing the attempted path in `returnUrl` and redirecting back upon successful login.
  - Header displays `Signed in as <Agent Name>` with a functioning `Sign out` action.

---

## Task 4 — Dockerise the API

### Containerization Strategy
- **Multi-Stage Dockerfile (DK-1, DK-3):**
  - Build stage based on `mcr.microsoft.com/dotnet/sdk:10.0`.
  - Dependency caching layer: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `.csproj` files are copied before executing `dotnet restore`. Source code is only copied subsequently, ensuring NuGet restore is cached across code changes.
  - Release binaries published using `dotnet publish -c Release -o /app/publish --no-restore`.
  - Runtime stage based on `mcr.microsoft.com/dotnet/aspnet:10.0`.
  - Non-root execution: runs under non-root user `app` (`USER $APP_UID`, UID 1654).
  - Listens on HTTP port 8080 (`ENV ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`). No TLS certificates inside the container.
- **Exclusions (DK-2):**
  - `.dockerignore` excludes `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, and `apps/web`.
- **Docker Compose Orchestration (DK-4, DK-5, DK-6):**
  - `docker-compose.yml` configures `api` service depending on `sqlserver` with `condition: service_healthy`.
  - Reaches SQL Server by container service name `Server=sqlserver,1433;...`.
  - Port published on host `localhost:5080`.
  - Environment variables injected from `.env.example` without hard-coded secrets.
  - On empty volume, API applies migrations and demo seed data automatically via `MigrateAndSeedAsync()`.
- **Health Check Endpoint (DK-7):**
  - `GET /health` endpoint tests database reachability via `CanConnectAsync()`, returning 200 OK (`{"status":"Healthy","database":"Connected"}`) or 503 Service Unavailable without requiring a JWT token.
- **Web App Proxy Integration (DK-8):**
  - The React client proxies to the containerized API using `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`.

---

## Verification & Test Results

| Test Category | Target Project / Suite | Tests Run | Result |
|---|---|---|---|
| Clean Architecture & EF Mappings | `SupportDesk.ArchitectureTests` | 4 | **4 Passed (0 failed)** |
| Domain, Application & Cryptography | `SupportDesk.UnitTests` | 75 | **75 Passed (0 failed)** |
| Web UI, Auth & Sla Components | `apps/web` (Vitest) | 24 | **24 Passed (0 failed)** |
| TypeScript Type Checking | `apps/web` (`tsc -b --noEmit`) | Full codebase | **0 Errors** |
| Frontend Code Quality | `apps/web` (`eslint .`) | Full codebase | **0 Errors, 0 Warnings** |
| **Total Automated Tests** | Solution-wide (`npm test`) | **103** | **103 Passed (100%)** |
