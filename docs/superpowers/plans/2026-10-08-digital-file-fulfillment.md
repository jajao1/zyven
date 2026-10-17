# Digital File Fulfillment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upload private digital assets and deliver authorized downloads after payment.

**Architecture:** Immutable asset metadata points to randomly named private files. Fulfillment definitions and executions snapshot the selected asset; download endpoints derive authorization from checkout or buyer session on every request.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, ASP.NET multipart uploads, React/TypeScript, Docker volumes.

---

### Task 1: Persist immutable digital assets
- [ ] Add `DigitalAsset`, optional asset references to fulfillment definition/execution, EF mappings and migration.
- [ ] Extend domain tests for valid digital delivery and idempotent execution constraints.
- [ ] Commit `feat: add digital asset fulfillment model`.

### Task 2: Secure storage, upload and download
- [ ] Add a private-store abstraction and local implementation using randomized keys.
- [ ] Validate 25 MiB maximum, declared MIME and PDF/ZIP/PNG/JPEG magic bytes while hashing the stream.
- [ ] Add seller upload/read endpoints and buyer/checkout download endpoints with entitlement revalidation.
- [ ] Add integration tests for invalid content, authorization and cross-buyer isolation.
- [ ] Commit `feat: add protected digital file delivery`.

### Task 3: Seller and buyer interfaces
- [ ] Add multipart client methods and a file editor with upload status and current asset metadata.
- [ ] Render digital files as authorized downloads in checkout and buyer library.
- [ ] Add frontend tests, responsive styles and documentation.
- [ ] Commit `feat: add digital file fulfillment interface`.

### Task 4: Verification and release
- [ ] Generate migration and confirm no pending model changes.
- [ ] Run `./scripts/test.ps1`, rebuild Compose, smoke endpoints and inspect the interface.
- [ ] Push to `feat/phase-1` and update PR #1.
