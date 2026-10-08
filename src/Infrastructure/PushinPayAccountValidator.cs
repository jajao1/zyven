using System.Net;
using System.Net.Http.Headers;
using Zyven.Application;

namespace Zyven.Infrastructure;

public sealed class PushinPayAccountValidator(HttpClient http) : IPushinPayAccountValidator
{
    public async Task<PaymentOperationError?> ValidateAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return PaymentOperationError.Rejected;
        using var request = new HttpRequestMessage(HttpMethod.Get, "balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.Add(new("application/json"));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) return null;
            return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? PaymentOperationError.Rejected
                : PaymentOperationError.Unavailable;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return PaymentOperationError.Unavailable; }
        catch (HttpRequestException) { return PaymentOperationError.Unavailable; }
    }
}
