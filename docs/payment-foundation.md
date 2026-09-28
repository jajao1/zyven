# Payment foundation (Phase 6 preparation only)

Phase 6 is not complete. Celcoin BaaS is the selected payment provider, but no public payment orchestration endpoint creates a charge yet and checkout continues to display payment unavailability. `UnconfiguredPaymentProcessor` advertises no capabilities and returns typed `Unavailable` without network or persistence operations until the contracted Celcoin product and production credentials are configured.

## Domain and money

Each organization created through `OrganizationService` receives one PENDING merchant in the same transaction as its membership/audit. The migration backfills existing organizations as PENDING; neither path approves merchants. The database enforces one merchant per organization and the specified status vocabulary.

`Payment.Prepare` is an in-memory PIX snapshot factory, currently unused by runtime request handlers. It requires an ACTIVE merchant in the same organization, a valid CREATED/unexpired checkout, and explicit fee values. All payment relationships, amount, currency and expiry come from the server snapshot. An adapter implementation will still need to lock/revalidate that snapshot and merchant status during orchestration.

The platform and provider fees are independent fixed BRL values under `Payments:Fees`. Defaults are `PlatformFixedFee=0.50` and `ProviderFixedFee=0.00`; Docker deployments can override them with `ZYVEN_PLATFORM_FEE` and `CELCOIN_TRANSACTION_FEE`. The Celcoin fee stays zero until the commercial contract provides its real value. Startup rejects negative values and fractional cents. `PaymentFeePolicy` also rejects a checkout whose gross value cannot cover both fees.

All monetary columns are decimal / PostgreSQL numeric(18,2). `PaymentAmounts` rejects negatives, overflow, fractional cents and combined fees above gross without rounding. GrossAmount means the final amount charged after adjustments. NetAmount = GrossAmount - PlatformFee - ProviderFee. DiscountAmount and OrderBumpAmount are informational components; this preparation sets both to zero. Discount/bump calculation rules belong to later phases. Database checks protect nonnegative amounts and the net equation; PostgreSQL numeric coercion alone does not reject fractional cents, so callers must use the validated domain factory before persistence/provider calls.

## Provider boundary

`IPaymentProcessor`, `IPixProvider` and `ICardProvider` describe transport-neutral operations. Creation requests copy a validated Payment and carry an immutable idempotency reference, monetary breakdown, currency and expiry. Card requests contain only an opaque token reference; no PAN/CVV fields exist. Query/cancellation identify the merchant, provider, external reference and optional transaction ID, so reconciliation can work when a timed-out create has no returned provider ID. Cancellation must only be attempted when advertised by the provider's capabilities. An `Indeterminate` outcome is distinct from a definitive rejection and must be reconciled before retrying.

Celcoin BaaS supports fixed-value and percentage split for dynamic PIX charges. Zyven will use a fixed split amount for its configurable platform fee. The live adapter remains disabled until the seller onboarding model, originator account, Pix key, merchant data, mTLS certificate, webhook authentication and reconciliation rules are known from the Celcoin contract.

Organizations link a Celcoin BaaS account through `PUT /api/organizations/{id}/payment-account`. Only owners and administrators may activate it. Account identifiers are unique across tenants and the mutation is audited. Linking an account records onboarding evidence; it does not enable payment creation by itself.

ExternalReference is unique per merchant, and provider transaction IDs are unique per (merchant, provider). IDs from different providers/accounts are not assumed globally unique. This is storage preparation, not a complete idempotent create/retry protocol or payment state machine. Provider responses must later be validated against the persisted amount/currency/expiry; a response alone must never mark an order paid.

## Persistence invariants and migration ownership

Payment foreign keys include OrganizationId for merchant, customer, checkout and offer. The migration also owns two database-only constraints: `AK_Checkouts_PaymentSnapshot` on (Id, OrganizationId, CustomerId, OfferId) and `FK_Payments_CheckoutSnapshot` from the corresponding Payment fields. These prevent valid-but-mismatched links within one organization as well as changes to referenced checkout relationships. They deliberately are not EF alternate keys: EF would otherwise prohibit changing checkout CustomerId before any payment exists. Contact identification remains mutable before payment and is protected by the database after a payment references it.

Future migrations changing these columns must preserve the custom constraints. Up adds the snapshot key/FK after creating Payments; Down drops Payments first and then the snapshot key. The EF snapshot describes the remaining schema and tenant keys. Integration coverage exercises empty database migration (normal fixture), upgrade with existing checkout/organizations, mutable customer before payment, rejected link updates after payment, status/money constraints and scoped uniqueness. Down/up runs only against a generated disposable test database.

## Required before real integration

Complete Celcoin BaaS contracting and homologation, then supply the client credentials, mTLS certificate, originator account, Pix key, merchant data, public webhook URL and seller account identifiers. Implement durable orchestration with network calls outside database transactions, safe recovery of unknown outcomes, webhook validation, statement-based reconciliation and real account evidence. Do not enable the UI/payment path or mark Phase 6 complete until that work passes. No Phase 7 webhook implementation is included here.
