# Sales and Finance Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tenant-safe sales and finance workspace with payment summaries, paginated sales, sale details, wallet balances, and ledger history.

**Architecture:** Add read-only sales contracts and a focused `SalesService` beside the existing ledger service. Expose authenticated finance endpoints protected by the same role policy, then add an isolated React feature and client under a role-aware workspace route.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, React, TypeScript, TanStack Query, Vitest, Testing Library.

---

### Task 1: Sales API contracts and query service

**Files:**
- Create: `src/Application/SalesContracts.cs`
- Create: `src/Infrastructure/SalesService.cs`
- Modify: `src/Api/Program.cs`
- Create: `src/Api/SalesEndpoints.cs`
- Test: `tests/IntegrationTests/SalesFinanceTests.cs`

- [ ] **Step 1: Write failing integration tests**

Create paid and pending payments in separate organizations and assert summary totals, sale projections, detail fields, status filtering, pagination, role restrictions, and tenant isolation through `/api/organizations/{id}/finance/summary` and `/sales`.

- [ ] **Step 2: Run the focused integration tests and verify endpoint failures**

Run `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~SalesFinanceTests` and expect failures because the routes do not exist.

- [ ] **Step 3: Add contracts and service**

Define `SalesSummaryResponse`, `SaleListItemResponse`, and `SaleDetailResponse`. Implement no-tracking projections filtered by `OrganizationId`, validate status and pagination, and reuse the `OWNER`/`ADMIN`/`FINANCE` access rule.

- [ ] **Step 4: Register endpoints and service**

Map summary, paginated sales, and sale detail under the existing finance prefix. Convert `OrganizationException` into the established problem responses.

- [ ] **Step 5: Run focused and full backend tests**

Run the focused test, then `dotnet test Zyven.slnx --no-restore`. Expect all tests to pass.

- [ ] **Step 6: Commit backend**

Commit as `feat(finance): expose sales reporting API`.

### Task 2: Frontend data client and sales workspace

**Files:**
- Create: `web/src/lib/sales-client.ts`
- Create: `web/src/Sales.tsx`
- Create: `web/src/Sales.test.tsx`
- Modify: `web/src/lib/catalog-navigation.ts`
- Modify: `web/src/Organizations.tsx`

- [ ] **Step 1: Write failing frontend tests**

Cover wallet metrics, sales formatting, status filter, pagination, detail loading, ledger expansion, retry behavior, and hiding the route from unauthorized roles.

- [ ] **Step 2: Run focused tests and verify failure**

Run `npm test -- --run src/Sales.test.tsx` and expect failure because the component and client do not exist.

- [ ] **Step 3: Implement the typed client**

Add types matching the API and methods for summary, sales, sale detail, wallet, and ledger. Use `authorizedRequest` and encode query parameters.

- [ ] **Step 4: Implement the workspace**

Build the Vendas and Extrato tabs with query keys containing user, organization, page, and filter. Format BRL values with `Intl.NumberFormat('pt-BR')`, show server statuses with Portuguese labels, and preserve empty/error/loading states.

- [ ] **Step 5: Wire role-aware navigation**

Add the `sales` route and render `Sales` only for `OWNER`, `ADMIN`, and `FINANCE`. Keep the API as the authoritative permission boundary.

- [ ] **Step 6: Run frontend tests**

Run `npm test -- --run`. Expect all tests to pass.

- [ ] **Step 7: Commit frontend behavior**

Commit as `feat(web): add sales finance workspace`.

### Task 3: Visual design and responsive behavior

**Files:**
- Modify: `web/src/index.css`
- Modify: `web/src/Sales.test.tsx`

- [ ] **Step 1: Add stable semantic selectors in tests**

Assert tab names, metric labels, table headings, sale detail dialog/region, and ledger entries through accessible roles and names.

- [ ] **Step 2: Implement Obsidian workspace styling**

Style the header, four metrics, tab bar, filter toolbar, responsive sales table, detail panel, and expandable ledger cards. At narrow widths, collapse rows into labeled blocks without horizontal overflow.

- [ ] **Step 3: Run test, build, and lint**

Run `npm test -- --run`, `npm run build`, and `npm run lint`. Expect all commands to pass.

- [ ] **Step 4: Commit design**

Commit as `feat(web): style sales finance dashboard`.

### Task 4: Documentation and end-to-end validation

**Files:**
- Modify: `docs/implementation-status.md`
- Create: `docs/sales-finance.md`
- Modify: `README.md`

- [ ] **Step 1: Document contracts and limits**

Describe endpoints, roles, status groups, monetary sources, immutable ledger behavior, and explicit exclusions such as withdrawals and refunds.

- [ ] **Step 2: Run repository validation**

Run `./scripts/test.ps1` and the applicable finance/payment smoke scripts. Expect build, tests, formatting, EF model verification, frontend checks, migrations, and smokes to pass.

- [ ] **Step 3: Rebuild the local environment**

Run `docker compose up -d --build`, confirm all services healthy, exercise authenticated finance endpoints, and inspect both dashboard tabs in the browser.

- [ ] **Step 4: Commit and push**

Commit documentation as `docs: document sales finance workspace` and push `HEAD` to `feat/phase-1`.
