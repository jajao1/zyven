using Zyven.Application;
namespace Zyven.Infrastructure;
// An explicit unavailable dependency, not a simulated provider. No network or persistence.
public sealed class UnconfiguredPaymentProcessor : IPaymentProcessor
{
    public PaymentCapabilities Capabilities => new(false, false, false);
    public Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unavailable));
    public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unavailable));
    public Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unavailable));
    public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unavailable));
}
