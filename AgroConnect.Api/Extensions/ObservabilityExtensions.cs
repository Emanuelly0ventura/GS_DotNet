using AgroConnect.Api.Observability;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgroConnect.Api.Extensions;

public static class ObservabilityExtensions
{
    public static void AddAgroConnectObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks().AddDbContextCheck<AgroConnectDbContext>("database", customTestQuery: async (db, ct) =>
        {
            await db.Users.AsNoTracking().AnyAsync(ct);
            return true;
        });
        builder.Services.AddHttpClient("external-health", client => client.Timeout = TimeSpan.FromSeconds(5));
        builder.Services.AddOptions<HealthCheckServiceOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            Register("recommendations", configuration["Services:AiHealthUrl"]);
            if (configuration.GetValue("Push:Enabled", false))
                Register("expo", configuration["Services:ExpoHealthUrl"] ?? "https://status.expo.dev/api/v2/status.json");

            void Register(string name, string? url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                    options.Registrations.Add(new HealthCheckRegistration(name,
                        services => new ExternalHealthCheck(uri, services.GetRequiredService<IHttpClientFactory>()), null, ["external"]));
            }
        });
    }

    public static void MapAgroConnectHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = (context, _) => context.Response.WriteAsJsonAsync(new { status = "ok" })
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
            })
        }).AllowAnonymous();
    }
}
