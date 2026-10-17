# PIX Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Schedule authorized e-mail or Telegram reminders for pending PIX payments and stop them as soon as the payment leaves the pending state.

**Architecture:** Versioned per-offer configuration creates one persistent execution after a PIX is attached. A Hangfire worker locks due executions, verifies payment state, reserves an idempotent attempt and dispatches through a channel abstraction. A hashed bot-journey reference binds Telegram checkout links to a private chat without trusting client-supplied IDs.

**Tech Stack:** .NET 10, EF Core 10/PostgreSQL, Hangfire, React/TypeScript, TanStack Query, React Hook Form.

---

### Task 1: Recovery domain and schema

**Files:**
- Create: `src/Domain/PixRecovery.cs`
- Create: `src/Infrastructure/PixRecoveryConfiguration.cs`
- Modify: `src/Infrastructure/ZyvenDbContext.cs`
- Test: `tests/UnitTests/PixRecoveryTests.cs`

- [ ] Add configuration, versioned message, execution, attempt and bot-reference entities with tenant composite foreign keys.
- [ ] Enforce one configuration per offer, one execution per payment, one attempt per execution/position and hashed unique bot references.
- [ ] Test valid schedules, maximum five messages, monotonic delays and terminal-state transitions.
- [ ] Generate migration `PixRecovery` and verify no pending model changes.
- [ ] Commit as `feat: add pix recovery persistence`.

### Task 2: Configuration API

**Files:**
- Create: `src/Application/PixRecoveryContracts.cs`
- Create: `src/Infrastructure/PixRecoverySettingsService.cs`
- Create: `src/Api/PixRecoveryEndpoints.cs`
- Modify: `src/Api/Program.cs`
- Test: `tests/IntegrationTests/PixRecoverySettingsTests.cs`

- [ ] Add tenant-authorized GET/PUT and publish endpoints below `/api/organizations/{org}/offers/{offer}/pix-recovery`.
- [ ] Validate 1–5 increasing delays, maximum 30 days, plain-text lengths and only `buyer_name`, `offer_name`, `amount`, `pix_expiry`, `pix_code`, and `checkout_url` variables.
- [ ] Copy drafts into immutable numbered versions on publish and audit saves/publications.
- [ ] Verify OWNER, ADMIN and OPERATOR writes, read-only financial/support roles and cross-tenant 404 responses.
- [ ] Commit as `feat: configure pix recovery schedules`.

### Task 3: Authorized channel origin

**Files:**
- Create: `src/Infrastructure/BotCheckoutReferenceService.cs`
- Modify: `src/Domain/Checkout.cs`
- Modify: `src/Application/CheckoutContracts.cs`
- Modify: `src/Infrastructure/PublicCheckoutService.cs`
- Modify: `src/Infrastructure/AutomationRunner.cs`
- Modify: `web/src/lib/page-client.ts`
- Modify: `web/src/PublicOfferPage.tsx`
- Test: `tests/IntegrationTests/PixRecoveryOriginTests.cs`

- [ ] When a Telegram automation sends an offer or checkout link, create a 256-bit random reference, persist only its SHA-256 hash with organization, offer, integration and Telegram user, and append the raw value to the Zyven URL.
- [ ] Let public checkout creation accept the opaque reference and consume it transactionally only when organization and offer match.
- [ ] Snapshot `OriginChannel`, `TelegramIntegrationId` and `TelegramUserId` on the checkout; otherwise snapshot `EMAIL`.
- [ ] Reject replay, expiry, cross-offer use and direct client-supplied Telegram identifiers.
- [ ] Commit as `feat: bind telegram journeys to checkouts`.

### Task 4: Scheduling and immediate cancellation

**Files:**
- Modify: `src/Infrastructure/PixPaymentService.cs`
- Modify: `src/Infrastructure/PushinPayWebhookService.cs`
- Create: `src/Infrastructure/PixRecoveryScheduler.cs`
- Test: `tests/IntegrationTests/PixRecoveryLifecycleTests.cs`

- [ ] Create one execution after `AttachPix` only when an enabled published configuration exists, selecting EMAIL for web checkouts and TELEGRAM for bound bot journeys.
- [ ] Store the published version and first due time without scheduling one Hangfire job per message.
- [ ] In the paid webhook transaction, mark a matching active execution `COMPLETED_PAYMENT` before commit.
- [ ] Test replay idempotency, no configuration, payment confirmation, expiration and checkout terminal states.
- [ ] Commit as `feat: schedule and cancel pix recovery`.

### Task 5: Delivery worker

**Files:**
- Create: `src/Infrastructure/TransactionalEmail.cs`
- Create: `src/Infrastructure/PixRecoveryWorker.cs`
- Modify: `src/Workers/Program.cs`
- Modify: `src/Api/Program.cs`
- Test: `tests/IntegrationTests/PixRecoveryWorkerTests.cs`

- [ ] Add `ITransactionalEmail` with an explicit development collector and a production-unconfigured implementation that fails closed.
- [ ] Add a non-overlapping Hangfire recurring worker that claims at most 100 due executions using PostgreSQL row locks with skip-locked semantics.
- [ ] Lock and recheck payment/checkout before creating the unique attempt; never send for non-pending or expired PIX.
- [ ] Expand safe variables and dispatch through e-mail or the organization bot. Mark success and advance; retry transient failures with bounded backoff; end permanent failures without channel fallback.
- [ ] Test successful e-mail/Telegram delivery, opt-out, transient retry, duplicate worker runs and the payment-versus-reservation race.
- [ ] Commit as `feat: deliver idempotent pix reminders`.

### Task 6: Seller editor and status

**Files:**
- Create: `web/src/PixRecoveryEditor.tsx`
- Create: `web/src/lib/pix-recovery-client.ts`
- Modify: `web/src/OfferPageEditor.tsx`
- Test: `web/src/PixRecoveryEditor.test.tsx`

- [ ] Add **Recuperação de PIX** to the offer actions with activation, ordered reminders, delay, e-mail and Telegram text.
- [ ] Provide the three default delays and supported-variable help, while enforcing five-message and 30-day limits in the form.
- [ ] Save drafts, publish versions and display current publication state and API errors.
- [ ] Test activation, editing, reordering, removal, validation and publication.
- [ ] Commit as `feat: add pix recovery editor`.

### Task 7: Verification and documentation

**Files:**
- Create: `docs/pix-recovery.md`
- Modify: `README.md`
- Modify: `docs/implementation-status.md`
- Modify: `docs/superpowers/plans/2026-10-16-pix-recovery.md`

- [ ] Apply all migrations to a clean PostgreSQL database.
- [ ] Run .NET build, unit/integration tests, format verification and EF pending-model check.
- [ ] Run frontend tests, production build and lint.
- [ ] Document channel selection, authorization, variables, retries, cancellation and required production e-mail configuration.
- [ ] Commit as `docs: document pix recovery` and push the existing PR branch.
