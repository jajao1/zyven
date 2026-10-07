# PushinPay PIX Payment Design

## Goal

Replace the active Celcoin integration with PushinPay while preserving the existing checkout, payment, immutable ledger, wallet, entitlement, and fulfillment flow. Each merchant connects its own PushinPay API token. The merchant account creates the PIX charge and automatically sends Zyven's configured fixed fee to Zyven's PushinPay account through `split_rules`.

Historical Celcoin payments remain readable. No migration rewrites their provider identifiers, webhook records, ledger transactions, or fulfillment history.

## Provider contract

PushinPay uses a Bearer token per merchant account. PIX creation uses `POST /pix/cashIn`; transaction verification uses `GET /transaction/{id}`. Production uses `https://api.pushinpay.com.br/api` and sandbox uses `https://api-sandbox.pushinpay.com.br/api`.

Amounts and split values are integer BRL cents. The minimum charge is 50 cents, and the documented total split limit is 50% of the charge. A successful creation returns the provider transaction ID, EMV `qr_code`, optional `qr_code_base64`, status, value, split rules, and eventual end-to-end ID. Provider statuses map as follows:

| PushinPay | Zyven |
| --- | --- |
| `created` | `PENDING` |
| `paid` | `PAID` |
| `expired` or `canceled` | `EXPIRED` or `CANCELLED` according to the received state |

The checkout displays the mandatory notice that PushinPay acts only as the payment processor and is not responsible for product delivery, support, content, quality, or seller obligations. The notice appears before PIX creation.

## Merchant credentials

`MerchantAccount` stores a PushinPay connection instead of Celcoin recipient and PIX presentation fields. The seller submits an API token through the authenticated organization payment-account endpoint. The API validates the credential against PushinPay before marking the account active.

The raw token is encrypted with AES-256-GCM using a deployment key supplied through `Payments:CredentialEncryptionKey`. The database stores ciphertext, nonce, tag, and a short non-secret fingerprint for support and rotation. API responses expose connection status and fingerprint only. Tokens never appear in logs, errors, frontend state after submission, audit payloads, or API responses.

The Zyven PushinPay `account_id` is deployment configuration. The platform API token is not used to create merchant charges. Rotating a merchant token replaces the encrypted credential atomically after validation.

## Charge creation and fees

The existing provider-neutral `IPaymentProcessor` remains the application boundary. Its PushinPay implementation receives the decrypted merchant token for one request, sends the gross checkout amount in cents, includes the callback URL, and includes one split rule whose value is Zyven's configured fixed platform fee in cents and whose `account_id` is the configured Zyven account.

The provider transaction fee remains separate from the Zyven fee in the payment and ledger model. It is configured only when known contractually; it is never inferred from the response. Charge creation is rejected locally when the gross value is below 50 cents, the configured fee does not fit the charge, or the platform split exceeds PushinPay's 50% limit.

PushinPay does not document an idempotency key for PIX creation. Zyven therefore persists one `PROCESSING` payment before the network call and never issues a blind second creation after an uncertain timeout. A definitive validation rejection marks the payment failed. A network or ambiguous provider failure leaves it processing for reconciliation.

## Webhook authentication and confirmation

Each merchant connection receives a random 256-bit callback secret. Only its SHA-256 hash is stored. PIX creation sends a callback URL containing the secret as an opaque route segment. The secret is generated and rotated by Zyven and is never supplied by the seller.

Receiving a webhook does not by itself authorize a financial transition. The handler:

1. Locates the merchant by the callback-secret hash without revealing whether it exists.
2. Validates the small JSON payload and provider transaction ID.
3. Deduplicates the event using a stable hash of merchant, provider transaction ID, status, and end-to-end ID.
4. Locks the matching payment row.
5. Queries `GET /transaction/{id}` with that merchant's token.
6. Confirms that the authoritative provider response is `paid`, its value equals the stored gross amount in cents, and it provides a non-empty end-to-end ID.
7. In one PostgreSQL transaction, marks the payment paid, completes checkout, records the ledger, creates the entitlement, creates fulfillment executions, and records the webhook event.

Only paid notifications trigger the verification query. Duplicate completed events return success without another provider query. This respects PushinPay's warning against polling and its documented one-query-per-minute limit.

## Data and ledger migration

The migration adds encrypted PushinPay credential fields and callback-secret hash to merchant accounts. Existing Celcoin connection fields remain temporarily nullable for historical compatibility, then application code stops reading and writing them. Existing active merchant accounts become disconnected until a PushinPay token is supplied; no Celcoin credential is treated as a PushinPay token.

The clearing account code changes from `CELCOIN_CLEARING` to `PAYMENT_PROCESSOR_CLEARING` for every organization. The migration updates the account code and name without changing ledger entries or balances. New organizations receive the neutral account chart. Historical ledger references remain intact because entries reference account IDs.

New payments persist provider `PUSHINPAY`. Historical payments retain their original provider. Webhook events use provider `PUSHINPAY`; existing Celcoin events remain unchanged.

## Configuration and UI

Deployment configuration contains:

- PushinPay enabled flag and production or sandbox base URL;
- Zyven PushinPay account ID;
- public HTTPS API base URL used to construct callbacks;
- 32-byte credential encryption key;
- configurable Zyven fixed fee and explicit provider fee.

The organization payment settings screen accepts a PushinPay token through a password field, submits it once, clears it immediately, and then shows connected state plus the fingerprint. Replacing a token requires entering a complete new token.

The checkout identifies PushinPay as the processor, displays the mandatory disclaimer before payment, and otherwise preserves the current QR Code, copy-and-paste, polling display, paid receipt, and delivery experience.

## Error handling and observability

Provider responses are size-limited and parsed with bounded JSON depth. Timeouts and transient 429/5xx responses do not create a second PIX. Validation and authentication failures return stable Portuguese messages without provider bodies or tokens. Logs include correlation ID, internal payment ID, provider transaction ID, HTTP status, and duration, but never credentials, callback secrets, payer documents, EMV codes, or raw webhook bodies.

Webhook endpoints return a generic success for unknown or already processed callbacks so they do not become an account-discovery oracle. Malformed payloads return 400. Provider verification failures are recorded for operational review without applying financial effects.

## Testing and completion criteria

Unit tests cover cent conversion, split payloads, token encryption and rotation, provider response mapping, required disclaimer, and rejection of invalid limits. Integration tests cover tenant-isolated credential connection, non-disclosure of tokens, one charge per checkout, authenticated callback lookup, authoritative provider verification, amount mismatch rejection, replay, concurrent notifications, and the complete payment-to-delivery transaction.

Migration tests cover existing organizations, neutral clearing-account rename, historical Celcoin payments, and disconnected legacy merchant accounts. Frontend tests cover token entry clearing, connected status, disclaimer visibility, QR Code display, paid state, and recovered delivery.

Completion requires clean .NET build and tests, EF model verification, frontend tests/build/lint, migration from an empty database and from the current schema, Docker Compose health, and a documented sandbox smoke procedure. Real sandbox completion remains dependent on seller PushinPay credentials and sandbox enablement by PushinPay support.
