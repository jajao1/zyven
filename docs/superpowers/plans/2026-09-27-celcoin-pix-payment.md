# Celcoin PIX Payment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver idempotent PIX charge creation and authenticated payment confirmation through Celcoin BaaS.

**Architecture:** A payment application service persists an authoritative checkout snapshot before calling a transport-only Celcoin adapter. Webhook processing is independently authenticated, deduplicated and amount checked. Public endpoints expose only the fields required to pay and poll.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, `HttpClient`, xUnit, React, TanStack Query.

---

### Task 1: Payment state machine and persistence

**Files:** `src/Domain/Payments.cs`, `src/Infrastructure/PaymentConfiguration.cs`, `src/Infrastructure/ZyvenDbContext.cs`, `tests/UnitTests/PaymentFoundationTests.cs`

- [ ] Write failing tests for PROCESSING, PENDING, PAID, FAILED, expiry and illegal transitions.
- [ ] Run the focused unit tests and confirm state behavior is missing.
- [ ] Add explicit domain transition methods and webhook event persistence.
- [ ] Add unique checkout payment and webhook event indexes.
- [ ] Run focused tests and commit.

### Task 2: Celcoin transport

**Files:** `src/Infrastructure/CelcoinPaymentProcessor.cs`, `src/Application/PaymentContracts.cs`, `src/Api/Program.cs`, `tests/UnitTests/CelcoinPaymentProcessorTests.cs`

- [ ] Write failing handler tests for OAuth, location request, split request, stable identifiers and response mapping.
- [ ] Run tests and confirm no Celcoin processor exists.
- [ ] Implement validated options, token caching, optional certificate loading and PIX-only capabilities.
- [ ] Classify timeout/5xx as indeterminate and 4xx as rejected.
- [ ] Run focused tests and commit.

### Task 3: Seller configuration and migration

**Files:** `src/Domain/Payments.cs`, `src/Application/OrganizationContracts.cs`, `src/Infrastructure/OrganizationService.cs`, `src/Infrastructure/PaymentConfiguration.cs`, `tests/IntegrationTests/PaymentFoundationTests.cs`

- [ ] Write a failing integration test for complete Celcoin account activation.
- [ ] Add validated account, PIX key and merchant identity fields.
- [ ] Generate and inspect the EF migration.
- [ ] Run integration tests and commit.

### Task 4: Public PIX orchestration

**Files:** `src/Infrastructure/PixPaymentService.cs`, `src/Application/PaymentContracts.cs`, `src/Api/PublicCheckoutEndpoints.cs`, `tests/IntegrationTests/PixPaymentTests.cs`

- [ ] Write failing integration tests for authorization, one charge per checkout and provider result persistence.
- [ ] Implement charge creation with checkout locking, a committed snapshot and an external provider call.
- [ ] Add POST and GET checkout payment endpoints using the existing checkout secret cookie.
- [ ] Run integration tests and commit.

### Task 5: Authenticated webhook

**Files:** `src/Infrastructure/CelcoinWebhookService.cs`, `src/Api/CelcoinWebhookEndpoints.cs`, `src/Api/Program.cs`, `tests/IntegrationTests/CelcoinWebhookTests.cs`

- [ ] Write failing tests for missing credentials, success, replay, unknown payment and mismatched amount.
- [ ] Implement constant-time Basic credential validation and bounded JSON parsing.
- [ ] Persist every unique event and atomically apply valid confirmations.
- [ ] Run integration tests and commit.

### Task 6: Buyer PIX experience

**Files:** `web/src/lib/page-client.ts`, `web/src/PublicOfferPage.tsx`, `web/src/PublicOfferPage.test.tsx`, `web/src/index.css`

- [ ] Write failing UI tests for charge request, copy code and paid polling result.
- [ ] Add typed payment client operations and the PIX receipt UI.
- [ ] Run frontend tests, lint and build, then commit.

### Task 7: Final validation

**Files:** `.env.example`, `compose.yaml`, `docs/payment-foundation.md`, `docs/celcoin-integration.md`

- [ ] Document every required Celcoin and webhook setting without real secrets.
- [ ] Run migrations against a disposable database and the full .NET test suite.
- [ ] Run frontend tests, lint and production build.
- [ ] Rebuild Docker Compose and verify API/frontend readiness.
- [ ] Inspect the complete diff, commit documentation and push the branch.
