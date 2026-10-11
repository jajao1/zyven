# Configurable Sales Funnel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let sellers build and publish an ordered Telegram sales/message funnel per offer.

**Architecture:** Tenant-scoped automation/version/step/execution records feed a bounded persistent runner. A simple React editor manages draft steps; publication snapshots an immutable version.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, Hangfire, React/TypeScript, Telegram Bot API.

---

### Task 1: Domain and migration
- [x] Add Automation, AutomationStep and AutomationExecution with tenant keys, statuses, step types and version invariants.
- [x] Add EF constraints/indexes and generate migration.
- [x] Add tests for publishing snapshots and step validation.

### Task 2: Draft and publication API
- [x] Add tenant-authorized GET/PUT endpoints under an offer.
- [x] Validate ordered steps, safe URLs, variables, condition targets and maximum sizes.
- [x] Publish by copying the draft into a new immutable version.
- [x] Add integration tests for tenant isolation.

### Task 3: Persistent runner
- [x] Add a runner for messages, offer/checkout actions, conditions, waits, Telegram access and end.
- [x] Persist cursor and retry state around external side effects.
- [x] Start PAYMENT_CONFIRMED executions from the protected buyer-to-Telegram link.
- [x] Preserve duplicate-event protection through the one-time link and unique execution key.

### Task 4: Seller editor
- [x] Add an Automations section to the offer editor.
- [x] Implement create/edit/delete/reorder controls and type-specific fields.
- [x] Add draft validation, publish status and tests.

### Task 5: Verification
- [x] Apply migration to a clean PostgreSQL database.
- [x] Run .NET build/tests/format/model checks and frontend tests/build/lint.
- [x] Document configuration and update the implementation map.
