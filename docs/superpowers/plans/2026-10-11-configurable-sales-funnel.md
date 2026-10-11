# Configurable Sales Funnel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let sellers build and publish an ordered Telegram sales/message funnel per offer.

**Architecture:** Tenant-scoped automation/version/step/execution records feed a bounded persistent runner. A simple React editor manages draft steps; publication snapshots an immutable version.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, Hangfire, React/TypeScript, Telegram Bot API.

---

### Task 1: Domain and migration
- [ ] Add Automation, AutomationStep and AutomationExecution with tenant keys, statuses, step types and version invariants.
- [ ] Add EF constraints/indexes and generate migration.
- [ ] Add unit tests for publishing snapshots and step validation.

### Task 2: Draft and publication API
- [ ] Add tenant-authorized GET/PUT endpoints under an offer.
- [ ] Validate ordered steps, safe URLs, variables, condition targets and maximum sizes.
- [ ] Publish by copying the draft into a new immutable version.
- [ ] Add integration tests for roles and tenant isolation.

### Task 3: Persistent runner
- [ ] Add a runner for messages, offer/checkout actions, conditions, waits, Telegram access and end.
- [ ] Persist cursor and external operation idempotency before side effects.
- [ ] Resume PAYMENT_CONFIRMED executions from the payment transaction/job.
- [ ] Add fake-Telegram tests for retries and duplicate events.

### Task 4: Seller editor
- [ ] Add an Automations section to the offer editor.
- [ ] Implement create/edit/delete/reorder controls and type-specific fields.
- [ ] Add draft validation, publish status and tests.

### Task 5: Verification
- [ ] Apply migration to a clean PostgreSQL database.
- [ ] Run .NET build/tests/format/model checks and frontend tests/build/lint.
- [ ] Document configuration and update the implementation map.
