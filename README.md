# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests
Run the automated tests from the repository root:

npm test

The latest individual test runs completed with:

Backend unit tests: 32 passed.

Frontend tests (Vitest): 5 passed.

Architecture tests: Could not be verified because Windows Application Control blocked the test assembly from loading.

The complete test suite has not been verified as passing. The architecture-test issue appears to be an environment policy restriction; it does not establish whether those tests would pass in an unrestricted environment.

Build and type-check verification should be run again before submission if any relevant source files have changed.
## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| GET /api/tickets | Paged ticket list with search, status, priority, categoryId, customerId, assignedAgentId, unassignedOnly, sortBy, and sortDirection. Filters are applied server-side in the database before counting and pagination. Search matches the ticket title, reference, or customer name; multiple filters combine with AND. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting
## Task 1 — Defect Fix and Server-Side Filtering

### DEFECT-117: Root cause and fix

The ticket list did not reliably reload with the latest filter values when filters changed. This could leave the displayed results stale or cause a request to use an outdated search value.

The fix updates the ticket-list hook so that filter changes trigger the appropriate reload. Search input is debounced by 300 ms to avoid a request on every keystroke, while other filter changes reload immediately. Regression tests cover status changes, changing status while a search debounce is pending, and clearing the search field.

### Filtering design decisions

- Filters are applied in the database query before counting and pagination.
- Status, priority, category ID, customer ID, and assigned-agent ID use exact-match filtering.
- `unassignedOnly` selects tickets that have no assigned agent.
- Search is trimmed and matches ticket title, reference, or customer name.
- Multiple active filters combine with AND.
- The total count reflects the filtered result set.

This keeps filtering and pagination on the server rather than fetching all tickets and filtering them in the browser.

## AI Usage

AI tools were used as development assistance. The submitted implementation and tests must be reviewed and understood by the candidate, who is responsible for explaining the code and design decisions.

## Known Limitations

- The Architecture test suite could not be verified because Windows Application Control blocked its test assembly from loading.
- The current environment reports moderate and high vulnerability warnings for `System.Security.Cryptography.Pkcs` version 9.0.11. The dependency should be reviewed before changing versions.
- Tasks 2–4 have not been verified as complete and should not be considered implemented based on this README.
- Out-of-scope features include registration, password reset, roles, OAuth/SSO, refresh tokens, MFA, notifications, CI, caching, and visual redesign.