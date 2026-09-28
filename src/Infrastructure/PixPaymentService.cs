using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class PixPaymentService(ZyvenDbContext db, IPaymentProcessor provider, PaymentFeePolicy fees, TimeProvider time)
{
    public async Task<PixPaymentResponse> Create(Guid checkoutId, string? secret, CancellationToken ct)
    {
        Payment payment;
        MerchantAccount merchant;
        Customer customer;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var checkout = await AuthorizedForUpdate(checkoutId, secret, ct);
            var existing = await db.Payments.SingleOrDefaultAsync(x => x.CheckoutSessionId == checkoutId, ct);
            if (existing is not null) { await transaction.CommitAsync(ct); return Response(existing); }
            merchant = await db.MerchantAccounts.SingleOrDefaultAsync(x => x.OrganizationId == checkout.OrganizationId, ct) ?? throw new OrganizationException(409, "A conta de pagamentos da organização não foi configurada.");
            if (merchant.Status != "ACTIVE" || string.IsNullOrWhiteSpace(merchant.PixKey) || string.IsNullOrWhiteSpace(merchant.MerchantName) || string.IsNullOrWhiteSpace(merchant.MerchantCity) || string.IsNullOrWhiteSpace(merchant.MerchantPostalCode)) throw new OrganizationException(409, "A conta Celcoin da organização está incompleta ou inativa.");
            customer = await db.Customers.SingleAsync(x => x.Id == checkout.CustomerId && x.OrganizationId == checkout.OrganizationId, ct);
            if (string.IsNullOrWhiteSpace(checkout.Document)) throw new OrganizationException(400, "Informe o CPF ou CNPJ para gerar o PIX.");
            PaymentFees configured;
            try { configured = fees.Calculate(checkout.Price); } catch (InvalidOperationException) { throw new OrganizationException(409, "O valor da oferta não comporta as taxas configuradas."); }
            payment = Payment.Prepare(checkout, merchant, configured.PlatformFee, time.GetUtcNow(), configured.ProviderFee); payment.BeginProvider(time.GetUtcNow()); db.Payments.Add(payment);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }

        var payer = new PaymentPayer(customer.Name, customer.Document ?? "", customer.Email, customer.Phone ?? "");
        var result = await provider.CreatePixAsync(new(payment, payer, merchant.ProviderRecipientId, merchant.PixKey, merchant.MerchantName, merchant.MerchantCity, merchant.MerchantPostalCode), ct);
        db.ChangeTracker.Clear(); payment = await db.Payments.SingleAsync(x => x.Id == payment.Id, ct);
        if (result.State is not null)
        {
            payment.AttachPix("CELCOIN", result.State.ProviderTransactionId, result.State.QrCodeData, result.State.PixCode ?? throw new InvalidOperationException("Celcoin did not return an EMV code."), result.State.ExpiresAt ?? payment.ExpiresAt, time.GetUtcNow());
            await db.SaveChangesAsync(ct); return Response(payment);
        }
        if (result.Error == PaymentOperationError.Rejected) { payment.Fail(time.GetUtcNow()); await db.SaveChangesAsync(ct); throw new OrganizationException(422, "A Celcoin rejeitou a criação desta cobrança PIX."); }
        throw new OrganizationException(503, "A confirmação da criação do PIX está pendente. Consulte novamente em instantes.");
    }

    public async Task<PixPaymentResponse> Read(Guid checkoutId, string? secret, CancellationToken ct)
    {
        await Authorized(checkoutId, secret, ct);
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutSessionId == checkoutId, ct) ?? throw new OrganizationException(404, "Pagamento PIX não encontrado.");
        return Response(payment);
    }

    private async Task<CheckoutSession> AuthorizedForUpdate(Guid id, string? secret, CancellationToken ct)
    {
        ValidateSecret(secret); var hash = Hash(secret!);
        var checkout = await db.Checkouts.FromSqlInterpolated($"SELECT * FROM \"Checkouts\" WHERE \"Id\" = {id} AND \"AccessHash\" = {hash} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Missing();
        ValidateCheckout(checkout); return checkout;
    }
    private async Task<CheckoutSession> Authorized(Guid id, string? secret, CancellationToken ct)
    {
        ValidateSecret(secret); var hash = Hash(secret!);
        var checkout = await db.Checkouts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.AccessHash == hash, ct) ?? throw Missing();
        if (checkout.Status is not ("CREATED" or "COMPLETED")) throw new OrganizationException(410, "Este checkout não está disponível.");
        return checkout;
    }
    private void ValidateCheckout(CheckoutSession checkout) { if (checkout.ExpiresAt <= time.GetUtcNow() || checkout.Status != "CREATED") throw new OrganizationException(410, "Este checkout expirou ou não está disponível."); }
    private static void ValidateSecret(string? secret) { if (secret is null || secret.Length != 96) throw Missing(); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static OrganizationException Missing() => new(404, "Checkout não encontrado.");
    private static PixPaymentResponse Response(Payment x) => new(x.Id, x.Status, x.GrossAmount.ToString("0.00", CultureInfo.InvariantCulture), x.Currency, x.PixCode, x.QrCodeData, x.ExpiresAt, x.PaidAt);
}
