# Implementation Approach — Northwind Support Desk

## Task 1 — Defect Fix and Server-Side Filtering

### DEFECT-117: Filter changes not reflected in the ticket list

The ticket-list hook did not correctly react to filter-state changes, causing stale results and requests that could use outdated filter values.

The fix updates the hook's reload behavior. Search input is debounced by 300 ms to avoid sending a request for every keystroke, while other filter changes trigger a reload immediately.

Regression tests cover status-filter reloads, changing status while a search debounce is pending, and clearing the search field.

### Server-side filtering

Ticket filters are applied in the database query before counting and pagination.

- Exact-match filters: status, priority, category ID, customer ID, and assigned-agent ID.
- `unassignedOnly`: returns tickets without an assigned agent.
- Search: matches the trimmed ticket title, reference, or customer name.
- Multiple filters combine with AND.
- Pagination metadata uses the filtered result count.

SQLite-backed tests cover exact priority matching, combined filters, customer-name search, assignment filters, category/customer filters, and pagination.

### Verification and limitations

- Backend unit tests: 32 passed in the latest run.
- Frontend tests: 5 passed in the latest run.
- Architecture tests could not be verified because Windows Application Control blocked the test assembly from loading.
- The current environment reports moderate and high vulnerability warnings for `System.Security.Cryptography.Pkcs` 9.0.11.

## Task 2 — Ticket Escalation and Assignment

**Status: Not yet verified.**

Review automatic triage, SLA calculations, specialist and capacity-based assignment, escalation history, frontend behavior, and tests before marking this task complete.

## Task 3 — Login and Authentication

**Status: Not yet verified.**

Review password hashing, JWT configuration and validation, protected endpoints, login UI, session handling, and tests before marking this task complete.

## Task 4 — Dockerise the API

**Status: Not yet verified.**

Review the multi-stage Dockerfile, runtime configuration, non-root execution, database migrations and seeding, health endpoint, and documented Docker verification before marking this task complete.
