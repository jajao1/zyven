# Financial Ledger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record every confirmed PIX in an immutable double-entry ledger and expose tenant-safe wallet and history reads.

**Architecture:** Organization-scoped accounts receive balanced entries generated from the authoritative Payment snapshot. Posting joins the existing webhook transaction, while PostgreSQL constraints and triggers enforce idempotency, tenant isolation, balance, and immutability.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL deferred constraint triggers, xUnit.

---

### Task 1: Ledger domain

**Files:** `src/Domain/Ledger.cs`, `tests/UnitTests/LedgerTests.cs`

- [ ] Write failing tests for balanced payment capture and invalid amounts.
- [ ] Implement accounts, transaction and immutable debit/credit entries.
- [ ] Run focused tests.

### Task 2: Persistence and migration

**Files:** `src/Infrastructure/LedgerConfiguration.cs`, `src/Infrastructure/ZyvenDbContext.cs`, `src/Infrastructure/Migrations/*FinancialLedger.cs`

- [ ] Configure composite tenant foreign keys and unique payment posting.
- [ ] Generate migration and add account/payment backfill.
- [ ] Add deferred balance and immutable mutation triggers.
- [ ] Apply migration to PostgreSQL.

### Task 3: Atomic webhook posting

**Files:** `src/Infrastructure/LedgerService.cs`, `src/Infrastructure/CelcoinWebhookService.cs`, `tests/IntegrationTests/PixPaymentTests.cs`

- [ ] Add a failing webhook test that expects balanced entries and replay-safe balance.
- [ ] Post the payment ledger inside the webhook transaction.
- [ ] Verify replay and database immutability.

### Task 4: Finance reads

**Files:** `src/Application/LedgerContracts.cs`, `src/Infrastructure/FinanceService.cs`, `src/Api/FinanceEndpoints.cs`, `tests/IntegrationTests/FinanceTests.cs`

- [ ] Add failing tenant and role authorization tests.
- [ ] Derive wallet totals from ledger entries.
- [ ] Add paginated ledger history.
- [ ] Run focused tests.

### Task 5: Validation

**Files:** `README.md`, `docs/implementation-status.md`, `docs/ledger.md`

- [ ] Document posting rules and finance endpoints.
- [ ] Run full .NET and frontend verification.
- [ ] Rebuild Docker, smoke test, commit and update the PR.
