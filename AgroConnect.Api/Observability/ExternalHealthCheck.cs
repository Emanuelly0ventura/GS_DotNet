using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgroConnect.Api.Observability;

public sealed class ExternalHealthCheck(Uri url, IHttpClientFactory clients) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient("external-health");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}
