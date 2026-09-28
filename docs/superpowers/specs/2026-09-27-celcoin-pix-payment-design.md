# Celcoin PIX Payment Design

## Outcome

An anonymous buyer with a valid checkout can request one PIX charge, receive its copy-and-paste code, poll its state, and see confirmation after an authenticated Celcoin webhook. The seller's Celcoin BaaS account originates the QR Code and Zyven receives its fixed configured fee through `feeInfo`.

## Boundaries

`PixPaymentService` owns checkout authorization, idempotent payment creation and state persistence. `CelcoinPaymentProcessor` owns OAuth, location creation and the immediate split API. `CelcoinWebhookService` validates and applies provider events. Provider network calls run outside database transactions. A unique payment per checkout and the persisted external reference prevent duplicate local charges.

## Seller and platform configuration

Each active merchant stores its Celcoin BaaS account number, PIX key, merchant name, city and postal code. Deployment configuration stores the Zyven BaaS account, Celcoin credentials, environment URL, optional sandbox certificate and required production mTLS certificate. The provider fee remains distinct from the Zyven platform fee.

## Creation flow

The API locks and validates the checkout, creates a PROCESSING payment snapshot, commits it, then calls Celcoin with stable request identifiers derived from the payment reference. The provider creates a COB location and an immediate charge with a fixed split to Zyven. A successful response stores transaction ID, transaction identification, EMV and expiry and changes the payment to PENDING. A definitive provider rejection changes it to FAILED. An uncertain network outcome stays PROCESSING for reconciliation and is never blindly recreated with different identifiers.

## Confirmation flow

The webhook endpoint uses configured Basic authentication, bounds the request size and accepts `pix-payment-in` only. It deduplicates on webhook ID, finds the payment by the original client request ID or provider transaction reference, verifies the gross amount exactly, and changes PENDING or PROCESSING to PAID once. Replays return success. Conflicting amounts are retained as rejected webhook events and do not change money state.

## User experience

After buyer details are saved, the checkout requests PIX and displays the QR image data derived locally from the returned EMV plus a copy button, amount, expiry and live status polling. Provider unavailability or merchant setup problems produce a clear retryable message without claiming a charge succeeded.

## Verification

Unit tests cover Celcoin payloads, response mapping, state transitions and webhook authentication/matching. Integration tests cover one-payment-per-checkout idempotency, tenant-safe persistence, amount mismatch rejection and replay. Frontend tests cover PIX display and paid state. Full .NET tests, frontend tests/lint/build, migrations and Docker smoke tests must pass.
