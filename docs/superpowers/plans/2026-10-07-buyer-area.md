# Buyer Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver passwordless buyer authentication and a unified, secure purchase library backed by existing payments, entitlements, and fulfillment executions.

**Architecture:** Buyer authentication is separate from seller JWT authentication and uses hashed one-time codes plus hashed opaque cookie sessions. A focused buyer service resolves purchases from the verified normalized email and rechecks payment and entitlement state for every read. React exposes login and library routes without entering the seller workspace.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, ASP.NET rate limiting, React, TypeScript, TanStack Query, React Hook Form, Zod, Vitest, Docker Compose.

---

### Task 1: Buyer authentication domain and persistence

**Files:**
- Create: `src/Domain/BuyerAccess.cs`
- Create: `src/Infrastructure/BuyerAccessConfiguration.cs`
- Modify: `src/Infrastructure/ZyvenDbContext.cs`
- Create: `src/Infrastructure/Migrations/<timestamp>_BuyerArea.cs`
- Test: `tests/UnitTests/BuyerAccessTests.cs`

- [ ] Write tests proving codes and sessions store hashes, expire, count failed attempts, reject reuse, and revoke.
- [ ] Run `dotnet test tests/UnitTests/UnitTests.csproj --filter BuyerAccessTests` and confirm the missing types fail compilation.
- [ ] Add `BuyerAccessCode` with normalized email, hash, expiration, attempts, consumed timestamp and methods `TryVerify`/`Consume`; add `BuyerSession` with token hash, normalized email, expiration and revocation.
- [ ] Configure lengths, indexes, UTC timestamps and concurrency token in EF Core; expose both sets on `ZyvenDbContext`.
- [ ] Generate the migration with `dotnet ef migrations add BuyerArea --project src/Infrastructure --startup-project src/Api`.
- [ ] Run the focused tests and `dotnet ef migrations has-pending-model-changes`; expect success and no pending changes.
- [ ] Commit as `feat: add buyer access persistence`.

### Task 2: Passwordless authentication service and endpoints

**Files:**
- Create: `src/Application/BuyerContracts.cs`
- Create: `src/Infrastructure/BuyerAuthService.cs`
- Create: `src/Infrastructure/BuyerCodeDelivery.cs`
- Create: `src/Api/BuyerEndpoints.cs`
- Modify: `src/Api/Program.cs`
- Test: `tests/IntegrationTests/BuyerAreaTests.cs`

- [ ] Write integration tests for neutral request responses, unknown emails, valid verification, invalid/expired/consumed codes, five-attempt lockout, session cookie, `me`, logout, and rate limiting.
- [ ] Run the focused integration class and confirm endpoint failures.
- [ ] Define request/response contracts and `IBuyerCodeDelivery`; implement development delivery through an in-memory test accessor and structured logs that never include production tokens.
- [ ] Implement code generation with `RandomNumberGenerator.GetInt32`, HMAC-SHA256 hashing with a configured server secret, transactional consumption, and 30-day opaque sessions.
- [ ] Map `/api/buyer/auth/request-code`, `/verify-code`, `/logout`, and `/api/buyer/me`; use one public error for every verification failure and set/clear the cookie centrally.
- [ ] Add named rate-limit policies for request and verification keyed by IP plus normalized e-mail hash.
- [ ] Run focused tests; expect all buyer authentication cases to pass.
- [ ] Commit as `feat: add passwordless buyer authentication`.

### Task 3: Unified purchase library API

**Files:**
- Create: `src/Infrastructure/BuyerPurchaseService.cs`
- Modify: `src/Application/BuyerContracts.cs`
- Modify: `src/Api/BuyerEndpoints.cs`
- Test: `tests/IntegrationTests/BuyerAreaTests.cs`

- [ ] Add tests with two buyers and two organizations proving only `PAID` payments with active entitlements are visible to the verified e-mail; cover pending payments, revoked access, unknown payment IDs, and duplicate customer records for the same e-mail.
- [ ] Run the focused test and confirm library endpoints are absent.
- [ ] Implement `List` and `Detail` projections joining customers, payments, offers, organizations, entitlements, definitions and executions by server-derived customer IDs.
- [ ] Return `/api/buyer/purchases` and `/api/buyer/purchases/{paymentId}`; reply 404 for foreign or inactive resources and never accept an e-mail from the client.
- [ ] Run the focused tests and confirm tenant and buyer isolation.
- [ ] Commit as `feat: add unified buyer purchase library`.

### Task 4: Buyer frontend

**Files:**
- Create: `web/src/lib/buyer-client.ts`
- Create: `web/src/BuyerArea.tsx`
- Create: `web/src/BuyerArea.test.tsx`
- Modify: `web/src/App.tsx`
- Modify: `web/src/PublicOfferPage.tsx`
- Modify: `web/src/index.css`

- [ ] Add Vitest cases for e-mail request, six-digit verification, invalid code, session restoration, empty library, populated library, logout, and external link rendering.
- [ ] Run `npm test -- BuyerArea.test.tsx` in `web`; confirm failures before implementation.
- [ ] Add a typed client using cookie credentials and neutral API errors.
- [ ] Implement `/buyer/login` and `/buyer/purchases` views with Zod forms, TanStack Query session restoration, purchase cards, loading/error/empty states and responsive layout matching the approved mockup.
- [ ] Add “Minhas compras” to the successful checkout state without altering immediate checkout delivery.
- [ ] Run focused tests, frontend build and lint; expect success.
- [ ] Commit as `feat: add buyer purchase experience`.

### Task 5: Documentation, smoke and final verification

**Files:**
- Create: `docs/buyer-area.md`
- Create: `scripts/smoke-buyer-area.ps1`
- Modify: `docs/implementation-status.md`
- Modify: `README.md`

- [ ] Document the access flow, cookies, expiration, security behavior, endpoints and production e-mail provider requirement.
- [ ] Add a smoke flow that creates a paid fixture through test infrastructure, requests and verifies a code, lists the purchase, opens its delivery and logs out.
- [ ] Run `./scripts/test.ps1`; require build, migration validation, all .NET/frontend tests, frontend build and lint to pass.
- [ ] Rebuild with `docker compose up -d --build api frontend worker`, run the buyer smoke and inspect desktop/mobile views.
- [ ] Commit as `docs: document buyer area`, push to `feat/phase-1`, and update PR #1.
