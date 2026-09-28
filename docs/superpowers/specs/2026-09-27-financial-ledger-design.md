# Financial Ledger Design

## Outcome

Every confirmed PIX creates exactly one balanced, immutable ledger transaction in the payment organization. The ledger is the authoritative source for the merchant's available balance and financial totals.

## Chart of accounts

Every organization owns four BRL accounts: `CELCOIN_CLEARING` (debit normal), `MERCHANT_AVAILABLE` (credit normal), `PLATFORM_FEE_REVENUE` (credit normal), and `PROVIDER_FEE_PAYABLE` (credit normal). A captured payment debits clearing by gross amount and credits the other accounts by net amount, platform fee, and provider fee. Debits always equal credits.

## Invariants

Ledger transactions and entries cannot be updated or deleted. PostgreSQL deferred triggers reject unbalanced transactions and mutation triggers reject updates/deletes. A unique payment key makes webhook replay idempotent. Every account, transaction and entry carries OrganizationId and uses composite foreign keys to prevent cross-tenant links.

Corrections will use new reversing transactions; this scope does not add refund handling. Existing paid payments are backfilled by the migration using the same entry rules.

## Posting and reads

The Celcoin webhook confirms the payment, completes the checkout, and posts the ledger transaction inside one database transaction. Failure rolls back all three effects. Finance endpoints derive wallet totals and ledger history from entries after checking persisted membership and finance access.

## Verification

Unit tests prove construction balances for fees including zero. Integration tests prove webhook replay does not duplicate balance, database immutability, balanced posting, migration backfill, and cross-tenant denial.
