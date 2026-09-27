# Seller Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a truthful, polished seller workspace with overview, indexed navigation, onboarding, and customer directory.

**Architecture:** Extend the existing URL store into a workspace route parser, keep server state in TanStack Query, and split overview and customer directory into focused components. Organizations owns tenant selection and renders one workspace destination at a time.

**Tech Stack:** React 19, TypeScript, TanStack Query, lucide-react, existing REST APIs and Swiss CSS system.

---

### Task 1: Workspace routing

**Files:**
- Modify: `web/src/lib/catalog-navigation.ts`
- Test: `web/src/lib/workspace-navigation.test.ts`

- [ ] Write route tests for overview, products, offers, customers, team, settings and catalog deep links.
- [ ] Run `npm test -- workspace-navigation.test.ts` and confirm the new destinations fail.
- [ ] Extend the parser and navigation helper while retaining the existing `kind`, `item`, and `org` contract.
- [ ] Re-run the focused test and commit.

### Task 2: Real overview and customers

**Files:**
- Create: `web/src/lib/customer-client.ts`
- Create: `web/src/WorkspaceOverview.tsx`
- Create: `web/src/WorkspaceOverview.test.tsx`
- Create: `web/src/Customers.tsx`
- Create: `web/src/Customers.test.tsx`

- [ ] Write tests that show API totals, honest payment unavailability, checklist actions, customer data, pagination, empty state, and retry.
- [ ] Run the focused tests and verify failure before components exist.
- [ ] Add the tenant-scoped client and components with query keys containing user and organization identities.
- [ ] Re-run focused tests and commit.

### Task 3: Workspace shell

**Files:**
- Modify: `web/src/Organizations.tsx`
- Modify: `web/src/Organizations.test.tsx`
- Modify: `web/src/App.tsx`
- Modify: `web/src/App.test.tsx`

- [ ] Update tests for overview landing, indexed navigation, deep-link compatibility, organization creation, team, settings, and logout.
- [ ] Run focused tests and confirm the old stacked page fails the new expectations.
- [ ] Refactor authenticated rendering into the selected-organization shell and route each destination to its focused component.
- [ ] Re-run focused tests and commit.

### Task 4: Swiss workspace styling and verification

**Files:**
- Modify: `web/src/index.css`
- Modify: `docs/implementation-status.md`
- Create: `docs/seller-workspace.md`

- [ ] Implement the numbered navigation rail, metric grid, checklist, directory, empty states, and responsive horizontal index using existing Swiss tokens.
- [ ] Run `npm test`, `npm run build`, and `npm run lint`.
- [ ] Run the repository full gate and rebuild Compose.
- [ ] Verify overview, customer directory, product/offer deep links, desktop layout, and 390 px layout in the browser.
- [ ] Commit documentation and final adjustments, then push and verify CI.

