# Configurable Sales Funnel Design

## Scope

Each offer may own one message funnel operated by the seller's Telegram bot. The first editor is an ordered form, not a free-form canvas. The persistence model remains channel-neutral so email and webhooks can be added later.

## Model

`Automation` is tenant- and offer-scoped, has name, trigger, status, current draft version and published version. `AutomationStep` belongs to a version and stores a stable order, type and validated configuration. `AutomationExecution` snapshots the published version, subject identifiers, current step, status and next run time. An execution never changes version after it starts.

Initial triggers are `TELEGRAM_STARTED` and `PAYMENT_CONFIRMED`. Initial steps are `SEND_TELEGRAM_MESSAGE`, `SEND_OFFER`, `CONDITION`, `WAIT`, `TELEGRAM_ACCESS` and `END`.

## Editor

The offer Automations section lets OWNER, ADMIN and OPERATOR create a funnel, add steps, edit, remove and reorder them, validate the draft and publish. Messages support plain text, safe variables and up to three URL/action buttons. Conditions initially inspect payment, checkout and entitlement status and choose explicit true/false next steps.

## Execution

Telegram deep links start or resume an execution. The runner processes immediate steps in order, persists before external calls, and schedules WAIT through Hangfire. Offer and checkout buttons use Zyven-owned URLs. Payment confirmation resumes matching executions. Telegram access reuses the protected invite service.

## Safety

Every read and mutation includes OrganizationId. Published versions are immutable. Execution and external-send keys are unique. The runner has a bounded step count, rejects cycles in a single run, validates variables and URLs, and never logs message variables, bot tokens, webhook secrets or invite links.

## MVP boundary

The first UI is a vertical list with move-up/move-down controls and a condition branch selector. Analytics, canvas edges, email and generic webhooks remain later expansions.
