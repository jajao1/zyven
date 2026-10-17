using Microsoft.Extensions.Logging;
namespace Zyven.Infrastructure;

public interface ITransactionalEmail { Task Send(string email, string subject, string text, CancellationToken ct); }
public sealed class DevelopmentTransactionalEmail(ILogger<DevelopmentTransactionalEmail> logger) : ITransactionalEmail { public Task Send(string email, string subject, string text, CancellationToken ct) { logger.LogInformation("Development PIX recovery email accepted for recipient fingerprint {Recipient}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email)))[..12]); return Task.CompletedTask; } }
public sealed class UnconfiguredTransactionalEmail : ITransactionalEmail { public Task Send(string email, string subject, string text, CancellationToken ct) => throw new InvalidOperationException("Transactional email provider is not configured."); }
