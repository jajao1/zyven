# PushinPay PIX Payment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the active Celcoin PIX integration with merchant-owned PushinPay tokens and automatic fixed-fee split to Zyven without changing the payment-to-ledger-to-delivery contract.

**Architecture:** Keep `IPaymentProcessor` as the application boundary, add a PushinPay transport and an application credential vault, and make webhook confirmation authoritative through a provider query. Preserve historical Celcoin rows while new charges, events, configuration, UI, and operational documentation use PushinPay.

**Tech Stack:** .NET 10, ASP.NET Core 10, EF Core 10, PostgreSQL 17, `HttpClient`, `System.Security.Cryptography`, xUnit, React 19, TypeScript, TanStack Query, React Hook Form, Zod, Docker Compose.

---

## File map

- `src/Domain/Payments.cs`: provider-neutral merchant connection fields and payment state.
- `src/Domain/Ledger.cs`: provider-neutral clearing account code.
- `src/Application/PaymentContracts.cs`: PushinPay transport and payment-account request/response contracts.
- `src/Infrastructure/PushinPayCredentialVault.cs`: AES-256-GCM encryption, decryption, fingerprints, and callback-secret hashing.
- `src/Infrastructure/PushinPayPaymentProcessor.cs`: create and query PushinPay transactions.
- `src/Infrastructure/PushinPayOptions.cs`: validated provider settings.
- `src/Infrastructure/OrganizationService.cs`: connect and rotate a merchant token after provider validation.
- `src/Infrastructure/PixPaymentService.cs`: pass merchant credentials to the processor and persist `PUSHINPAY` results.
- `src/Infrastructure/PushinPayWebhookService.cs`: callback authentication, deduplication, authoritative verification, and atomic completion.
- `src/Api/PushinPayWebhookEndpoints.cs`: bounded public webhook endpoint.
- `src/Api/Program.cs`: provider, options, vault, client, and endpoint registration.
- `src/Infrastructure/Migrations/*_PushinPayGateway.cs`: schema transition, legacy disconnect, and clearing-account rename.
- `web/src/Organizations.tsx` and client modules: token connection UI and masked connected state.
- `web/src/PublicOfferPage.tsx`: required PushinPay processor notice.
- `compose.yaml`, `.env.example`, docs, and smoke scripts: deployment and sandbox instructions.

### Task 1: Neutralize the financial core

**Files:**
- Modify: `src/Domain/Ledger.cs`
- Modify: `src/Infrastructure/LedgerService.cs`
- Modify: `tests/UnitTests/LedgerTests.cs`
- Test: `tests/IntegrationTests/PaymentFoundationTests.cs`

- [ ] **Step 1: Write a failing unit test for the neutral clearing account**

```csharp
[Fact]
public void Organization_chart_uses_provider_neutral_clearing()
{
    var accounts = LedgerAccount.CreateChart(Guid.NewGuid(), DateTimeOffset.UtcNow);
    Assert.Contains(accounts, x => x.Code == "PAYMENT_PROCESSOR_CLEARING");
    Assert.DoesNotContain(accounts, x => x.Code == "CELCOIN_CLEARING");
}
```

- [ ] **Step 2: Run the test and verify the expected failure**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter Organization_chart_uses_provider_neutral_clearing`

Expected: FAIL because the chart still contains `CELCOIN_CLEARING`.

- [ ] **Step 3: Rename the domain constant and all current-ledger references**

```csharp
public static class LedgerAccountCodes
{
    public const string PaymentProcessorClearing = "PAYMENT_PROCESSOR_CLEARING";
    public const string MerchantAvailable = "MERCHANT_AVAILABLE";
    public const string PlatformFeeRevenue = "PLATFORM_FEE_REVENUE";
    public const string ProviderFeePayable = "PROVIDER_FEE_PAYABLE";
}
```

Use `PaymentProcessorClearing` in chart creation, capture entries, wallet totals, and ledger tests.

- [ ] **Step 4: Run ledger tests**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter LedgerTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Domain/Ledger.cs src/Infrastructure/LedgerService.cs tests/UnitTests/LedgerTests.cs
git commit -m "refactor(finance): neutralize payment clearing account"
```

### Task 2: Add encrypted merchant credentials

**Files:**
- Create: `src/Infrastructure/PushinPayCredentialVault.cs`
- Modify: `src/Domain/Payments.cs`
- Modify: `src/Infrastructure/PaymentConfiguration.cs`
- Create: `tests/UnitTests/PushinPayCredentialVaultTests.cs`

- [ ] **Step 1: Write failing round-trip, non-disclosure, tamper, and key-length tests**

```csharp
[Fact]
public void Vault_encrypts_and_authenticates_tokens()
{
    var vault = new PushinPayCredentialVault(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    var encrypted = vault.Encrypt("merchant-secret-token");
    Assert.DoesNotContain("merchant-secret-token", encrypted.Ciphertext);
    Assert.Equal("merchant-secret-token", vault.Decrypt(encrypted));
    Assert.Equal(12, encrypted.Fingerprint.Length);
}

[Fact]
public void Vault_rejects_modified_ciphertext()
{
    var vault = TestVault();
    var encrypted = vault.Encrypt("merchant-secret-token");
    var changed = encrypted with { Ciphertext = encrypted.Ciphertext[..^2] + "AA" };
    Assert.Throws<CryptographicException>(() => vault.Decrypt(changed));
}
```

- [ ] **Step 2: Run tests and verify they fail because the vault is absent**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter PushinPayCredentialVaultTests`

Expected: build failure for missing `PushinPayCredentialVault`.

- [ ] **Step 3: Implement AES-256-GCM with a random nonce**

```csharp
public sealed record EncryptedCredential(string Ciphertext, string Nonce, string Tag, string Fingerprint);

public sealed class PushinPayCredentialVault
{
    private readonly byte[] key;
    public PushinPayCredentialVault(string base64Key)
    {
        key = Convert.FromBase64String(base64Key);
        if (key.Length != 32) throw new InvalidOperationException("Payments:CredentialEncryptionKey must decode to 32 bytes.");
    }
    public EncryptedCredential Encrypt(string token)
    {
        var plain = Encoding.UTF8.GetBytes(token.Trim());
        var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag);
        var fingerprint = Convert.ToHexString(SHA256.HashData(plain))[..12];
        return new(Convert.ToBase64String(cipher), Convert.ToBase64String(nonce), Convert.ToBase64String(tag), fingerprint);
    }
    public string Decrypt(EncryptedCredential value)
    {
        var cipher = Convert.FromBase64String(value.Ciphertext); var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16); aes.Decrypt(Convert.FromBase64String(value.Nonce), cipher, Convert.FromBase64String(value.Tag), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
```

Add private-set merchant properties for ciphertext, nonce, tag, fingerprint, callback-secret hash, and connection status. Provide domain methods that accept encrypted values but never expose a raw token.

- [ ] **Step 4: Run credential tests**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter PushinPayCredentialVaultTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Domain/Payments.cs src/Infrastructure/PaymentConfiguration.cs src/Infrastructure/PushinPayCredentialVault.cs tests/UnitTests/PushinPayCredentialVaultTests.cs
git commit -m "feat(payments): encrypt merchant PushinPay tokens"
```

### Task 3: Implement the PushinPay transport

**Files:**
- Create: `src/Infrastructure/PushinPayOptions.cs`
- Create: `src/Infrastructure/PushinPayPaymentProcessor.cs`
- Modify: `src/Application/PaymentContracts.cs`
- Create: `tests/UnitTests/PushinPayPaymentProcessorTests.cs`

- [ ] **Step 1: Write failing request and response contract tests**

```csharp
[Fact]
public async Task Create_sends_integer_cents_webhook_and_platform_split()
{
    var handler = new RecordingHandler("""{"id":"tx-1","qr_code":"000201","status":"created","value":1990,"qr_code_base64":"data:image/png;base64,AA=="}""");
    var processor = Processor(handler, platformAccount: "zyven-account");
    var result = await processor.CreatePixAsync(Request(gross: 19.90m, fee: .50m, token: "seller-token", callback: "https://api.example.test/api/webhooks/pushinpay/secret"), default);
    Assert.Equal("Bearer seller-token", handler.Authorization);
    Assert.Equal(1990, handler.Json.RootElement.GetProperty("value").GetInt32());
    Assert.Equal(50, handler.Json.RootElement.GetProperty("split_rules")[0].GetProperty("value").GetInt32());
    Assert.Equal("zyven-account", handler.Json.RootElement.GetProperty("split_rules")[0].GetProperty("account_id").GetString());
    Assert.Equal("tx-1", result.State!.ProviderTransactionId);
}
```

Add tests for 422 rejection, 401 invalid token, 429/transient uncertainty, malformed oversized JSON, query mapping, minimum 50 cents, and split over 50%.

- [ ] **Step 2: Run tests and verify the transport is missing**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter PushinPayPaymentProcessorTests`

Expected: build failure for missing PushinPay types.

- [ ] **Step 3: Define provider-neutral credential-aware requests**

```csharp
public sealed record PaymentProviderCredential(string Token, string CallbackUrl);
public sealed record PaymentChargeRequest(Payment Payment, PaymentPayer Payer, PaymentProviderCredential Credential);
public sealed record PaymentLookup(string ProviderTransactionId, string Token);
```

Keep the existing `PaymentOperationResult`; map `qr_code` to `PixCode`, `qr_code_base64` to `QrCodeData`, and cents to exact decimal BRL.

- [ ] **Step 4: Implement create and query calls**

Use `POST pix/cashIn` and `GET transaction/{Uri.EscapeDataString(id)}` relative to a validated HTTPS base address. Set Bearer authentication per request rather than on shared client defaults. Use `JsonDocument.ParseAsync` with `MaxDepth = 24` after enforcing a 256 KiB response limit.

- [ ] **Step 5: Run transport tests**

Run: `dotnet test tests/UnitTests/UnitTests.csproj --filter PushinPayPaymentProcessorTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Application/PaymentContracts.cs src/Infrastructure/PushinPayOptions.cs src/Infrastructure/PushinPayPaymentProcessor.cs tests/UnitTests/PushinPayPaymentProcessorTests.cs
git commit -m "feat(payments): add PushinPay PIX transport"
```

### Task 4: Connect merchant PushinPay accounts

**Files:**
- Modify: `src/Application/OrganizationContracts.cs`
- Modify: `src/Infrastructure/OrganizationService.cs`
- Modify: `src/Api/OrganizationEndpoints.cs`
- Modify: `tests/IntegrationTests/OrganizationTests.cs`

- [ ] **Step 1: Write failing integration tests for token connection and isolation**

```csharp
[Fact]
public async Task Payment_account_validates_encrypts_and_never_returns_token()
{
    var response = await owner.PutAsJsonAsync($"/api/organizations/{org}/payment-account", new { token = "seller-secret" });
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("seller-secret", json);
    var stored = await db.MerchantAccounts.SingleAsync(x => x.OrganizationId == org);
    Assert.NotEqual("seller-secret", stored.CredentialCiphertext);
    Assert.Equal("ACTIVE", stored.Status);
}
```

Cover provider rejection, token rotation, `OPERATOR`/`FINANCE` denial, and a user from another organization receiving 404.

- [ ] **Step 2: Run tests and verify the request contract rejects the new shape**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter Payment_account_validates_encrypts_and_never_returns_token`

Expected: FAIL because the endpoint still expects Celcoin fields.

- [ ] **Step 3: Replace the public contract**

```csharp
public sealed record PaymentAccountRequest(string Token);
public sealed record PaymentAccountResponse(string Status, string Provider, string TokenFingerprint);
```

Add a provider validation operation that performs an authenticated, read-only PushinPay request. Generate a 32-byte callback secret, persist only its SHA-256 hash, encrypt the token, and audit `payment_account.pushinpay_connected`.

- [ ] **Step 4: Run organization integration tests**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter OrganizationTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/Application/OrganizationContracts.cs src/Infrastructure/OrganizationService.cs src/Api/OrganizationEndpoints.cs tests/IntegrationTests/OrganizationTests.cs
git commit -m "feat(payments): connect merchant PushinPay accounts"
```

### Task 5: Create PushinPay PIX charges through the existing checkout

**Files:**
- Modify: `src/Infrastructure/PixPaymentService.cs`
- Modify: `src/Domain/Payments.cs`
- Modify: `src/Api/Program.cs`
- Modify: `tests/IntegrationTests/PixPaymentTests.cs`

- [ ] **Step 1: Change the checkout integration test to expect PushinPay**

```csharp
var stored = await db.Payments.SingleAsync(x => x.CheckoutSessionId == checkout.Id);
Assert.Equal("PUSHINPAY", stored.Provider);
Assert.Equal("tx-1", stored.ProviderTransactionId);
Assert.Equal("000201-pix", stored.PixCode);
Assert.Equal(1, provider.Calls);
```

Assert that an inactive or disconnected merchant returns 409 and an uncertain provider result remains `PROCESSING` without a second provider call.

- [ ] **Step 2: Run the focused tests and verify they fail on Celcoin assumptions**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter Checkout_creates_one_pix_charge_and_returns_it_idempotently`

Expected: FAIL because the service still requires Celcoin merchant fields and stores `CELCOIN`.

- [ ] **Step 3: Pass decrypted credentials only at the network boundary**

Build the callback URL from validated `PublicApiBaseUrl`, merchant ID, and the one-time raw callback secret returned only during connection/rotation. Persist a protected callback secret that can be recovered for charge creation, plus its hash for lookup; do not attempt to derive the raw secret from its hash.

Call the processor with `PaymentProviderCredential`, store provider `PUSHINPAY`, and replace Celcoin-specific Portuguese errors with PushinPay messages.

- [ ] **Step 4: Register PushinPay and validated options**

```csharp
builder.Services.AddOptions<PushinPayOptions>()
    .Bind(builder.Configuration.GetSection(PushinPayOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<IPaymentProcessor, PushinPayPaymentProcessor>();
builder.Services.AddSingleton<PushinPayCredentialVault>();
```

- [ ] **Step 5: Run payment tests**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PixPaymentTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Domain/Payments.cs src/Infrastructure/PixPaymentService.cs src/Api/Program.cs tests/IntegrationTests/PixPaymentTests.cs
git commit -m "feat(payments): create checkout PIX with PushinPay"
```

### Task 6: Replace the webhook with authoritative PushinPay verification

**Files:**
- Create: `src/Infrastructure/PushinPayWebhookService.cs`
- Create: `src/Api/PushinPayWebhookEndpoints.cs`
- Delete: `src/Infrastructure/CelcoinWebhookService.cs`
- Delete: `src/Api/CelcoinWebhookEndpoints.cs`
- Modify: `src/Api/Program.cs`
- Modify: `tests/IntegrationTests/PixPaymentTests.cs`

- [ ] **Step 1: Write failing webhook tests**

Test these observable cases:

```csharp
Assert.Equal(HttpStatusCode.OK, await Post("unknown-secret", paidPayload));
Assert.Equal("PENDING", await PaymentStatus());

await Post(validSecret, paidPayload);
Assert.Equal("PAID", await PaymentStatus());
Assert.Single(await db.LedgerTransactions.Where(x => x.PaymentId == paymentId).ToListAsync());
Assert.Single(await db.Entitlements.Where(x => x.PaymentId == paymentId).ToListAsync());
```

Add amount mismatch, provider status mismatch, missing end-to-end ID, replay, two concurrent distinct notifications, malformed JSON, and cross-merchant transaction tests. Assert the fake provider query is invoked once for the first paid notification and zero times for a completed replay.

- [ ] **Step 2: Run webhook tests and verify the route is missing**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PushinPay_webhook`

Expected: FAIL with 404 for `/api/webhooks/pushinpay/{secret}`.

- [ ] **Step 3: Implement callback lookup and stable event identity**

Hash the route secret, load the active merchant, parse `id`, `value`, `status`, and `end_to_end_id`, then compute:

```csharp
var eventIdentity = $"{merchant.Id:N}:{providerId}:{status}:{endToEndId}";
var externalEventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(eventIdentity)));
```

Take a PostgreSQL advisory transaction lock on that identity, return duplicate success before querying when an applied event already exists, and lock the payment row before changes.

- [ ] **Step 4: Query PushinPay before financial effects**

Decrypt the merchant token, call `QueryAsync`, require `paid`, exact cents, matching provider transaction ID, and a non-empty end-to-end ID. Then reuse the existing ledger and fulfillment services inside the same database transaction.

- [ ] **Step 5: Run webhook and full integration tests**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PixPaymentTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/Infrastructure/PushinPayWebhookService.cs src/Api/PushinPayWebhookEndpoints.cs src/Api/Program.cs tests/IntegrationTests/PixPaymentTests.cs
git rm src/Infrastructure/CelcoinWebhookService.cs src/Api/CelcoinWebhookEndpoints.cs
git commit -m "feat(payments): verify PushinPay webhooks authoritatively"
```

### Task 7: Add the database migration

**Files:**
- Create: `src/Infrastructure/Migrations/*_PushinPayGateway.cs`
- Modify: `src/Infrastructure/Migrations/ZyvenDbContextModelSnapshot.cs`
- Modify: `tests/IntegrationTests/PaymentFoundationTests.cs`

- [ ] **Step 1: Extend the upgrade test before generating the migration**

After upgrading a database at the current migration, assert:

```csharp
Assert.All(await db.MerchantAccounts.ToListAsync(), x => Assert.Equal("PENDING", x.Status));
Assert.All(await db.LedgerAccounts.Where(x => x.Code == "PAYMENT_PROCESSOR_CLEARING").ToListAsync(), x => Assert.Equal("Payment processor clearing", x.Name));
Assert.DoesNotContain(await db.LedgerAccounts.ToListAsync(), x => x.Code == "CELCOIN_CLEARING");
Assert.Equal("CELCOIN", historicalPayment.Provider);
```

- [ ] **Step 2: Run the upgrade test and verify it fails**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter Upgrade_from_customer_schema_preserves_data_and_adds_payment_constraints`

Expected: FAIL because the PushinPay credential columns and neutral clearing code do not exist.

- [ ] **Step 3: Generate and harden the migration**

Run:

```powershell
dotnet tool run dotnet-ef migrations add PushinPayGateway --project src/Infrastructure --startup-project src/Api
```

Add SQL to rename `CELCOIN_CLEARING` to `PAYMENT_PROCESSOR_CLEARING`, rename the account, clear legacy active connection state to `PENDING`, and leave historical payment/event provider values unchanged. Add unique filtered index for callback-secret hash and check constraints requiring all encrypted credential components together.

- [ ] **Step 4: Run migration and model checks**

Run:

```powershell
dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PaymentFoundationTests
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Api
```

Expected: all tests PASS and command reports no pending model changes.

- [ ] **Step 5: Commit**

```powershell
git add src/Infrastructure/Migrations tests/IntegrationTests/PaymentFoundationTests.cs
git commit -m "feat(payments): migrate merchant accounts to PushinPay"
```

### Task 8: Update merchant and checkout interfaces

**Files:**
- Modify: `web/src/lib/organization-client.ts`
- Modify: `web/src/Organizations.tsx`
- Modify: `web/src/Organizations.test.tsx`
- Modify: `web/src/PublicOfferPage.tsx`
- Modify: `web/src/PublicOfferPage.test.tsx`

- [ ] **Step 1: Write failing frontend tests**

```tsx
fireEvent.change(screen.getByLabelText('Token PushinPay'), { target: { value: 'seller-secret' } })
fireEvent.click(screen.getByRole('button', { name: 'Conectar PushinPay' }))
await screen.findByText(/PushinPay conectada/)
expect(screen.queryByDisplayValue('seller-secret')).not.toBeInTheDocument()
```

On the public checkout, assert the exact required processor notice is visible before the “Gerar PIX” button and remains visible on the payment step.

- [ ] **Step 2: Run tests and verify the new controls and notice are missing**

Run: `npm test -- --run src/Organizations.test.tsx src/PublicOfferPage.test.tsx`

Expected: FAIL on missing token label and processor notice.

- [ ] **Step 3: Implement the one-way token form**

Use an `input type="password"`, Zod minimum/maximum validation, mutation through the organization client, and `reset()` after success. Display only provider, status, and fingerprint returned by the API.

- [ ] **Step 4: Add the checkout notice**

Render this text before PIX creation:

```text
A PushinPay atua exclusivamente como processadora de pagamentos e não possui responsabilidade pela entrega, suporte, conteúdo, qualidade ou cumprimento das obrigações relacionadas aos produtos ou serviços oferecidos pelo vendedor.
```

- [ ] **Step 5: Run frontend tests, build, and lint**

Run:

```powershell
npm test -- --run src/Organizations.test.tsx src/PublicOfferPage.test.tsx
npm run build
npm run lint
```

Expected: PASS with no lint errors.

- [ ] **Step 6: Commit**

```powershell
git add web/src
git commit -m "feat(web): connect PushinPay and disclose processor role"
```

### Task 9: Replace deployment configuration and documentation

**Files:**
- Modify: `compose.yaml`
- Modify: `.env.example`
- Modify: `README.md`
- Create: `docs/pushinpay-integration.md`
- Modify: `docs/implementation-status.md`
- Delete: `docs/celcoin-integration.md`
- Modify: `scripts/smoke-payment-foundation.ps1`

- [ ] **Step 1: Add a configuration validation test**

Start the API with PushinPay enabled and each required setting absent in turn. Assert startup fails for missing platform account ID, public HTTPS API URL, or 32-byte credential encryption key. Assert an HTTP provider base URL is rejected.

- [ ] **Step 2: Run the validation test and verify current Celcoin settings do not satisfy it**

Run: `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PushinPay_configuration`

Expected: FAIL because PushinPay options are not fully wired.

- [ ] **Step 3: Replace environment variables**

Document and wire:

```text
PUSHINPAY_ENABLED
PUSHINPAY_BASE_URL
PUSHINPAY_PLATFORM_ACCOUNT_ID
PUBLIC_API_BASE_URL
PAYMENT_CREDENTIAL_ENCRYPTION_KEY
ZYVEN_PLATFORM_FEE
PUSHINPAY_TRANSACTION_FEE
```

Generate `PAYMENT_CREDENTIAL_ENCRYPTION_KEY` as 32 random bytes encoded in Base64 in `scripts/setup.ps1`. Remove active Celcoin variables from Compose and `.env.example`.

- [ ] **Step 4: Document sandbox operation**

Explain merchant token connection, PushinPay support activation for sandbox, automatic split, callback URL, required checkout notice, webhook verification, rate limit, secret rotation, and how to run a sandbox payment without committing credentials.

- [ ] **Step 5: Run configuration tests and smoke script**

Run:

```powershell
dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter PushinPay_configuration
./scripts/smoke-payment-foundation.ps1
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add compose.yaml .env.example README.md docs scripts tests/IntegrationTests
git rm docs/celcoin-integration.md
git commit -m "docs(payments): configure PushinPay environments"
```

### Task 10: Remove stale Celcoin implementation and verify the release

**Files:**
- Delete: `src/Infrastructure/CelcoinPaymentProcessor.cs`
- Delete: `src/Infrastructure/CelcoinOptions.cs`
- Delete: `tests/UnitTests/CelcoinPaymentProcessorTests.cs`
- Modify: remaining files returned by the stale-reference scan

- [ ] **Step 1: Scan active code and configuration for stale references**

Run:

```powershell
rg -n "Celcoin|CELCOIN|celcoin" src web compose.yaml .env.example README.md docs --glob '!src/Infrastructure/Migrations/**' --glob '!docs/superpowers/**'
```

Expected: only explicit historical compatibility statements remain in documentation; no active code or configuration references remain.

- [ ] **Step 2: Remove obsolete classes and tests**

Delete the Celcoin transport, options, and provider-specific tests. Keep historical migrations unchanged because deployed migration history is immutable.

- [ ] **Step 3: Run the complete reproducible validation**

Run: `./scripts/test.ps1`

Expected: .NET build, all unit and integration tests, formatting, EF model check, frontend install/tests/build/lint, and ephemeral database migrations all pass.

- [ ] **Step 4: Rebuild and inspect the local environment**

Run:

```powershell
docker compose up -d --build
docker compose ps
Invoke-RestMethod http://localhost:5080/health/ready
```

Expected: API and frontend healthy, migration container exited successfully, PostgreSQL and Redis healthy, readiness returns `{"status":"healthy"}`.

- [ ] **Step 5: Commit final cleanup**

```powershell
git add -A
git commit -m "refactor(payments): remove active Celcoin integration"
git push
```

- [ ] **Step 6: Update the pull request**

Change its title and description to describe merchant-owned PushinPay tokens, automatic Zyven split, authoritative webhook verification, migration behavior, and the final validation counts.
