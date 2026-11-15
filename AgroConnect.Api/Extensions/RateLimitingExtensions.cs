using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgroConnect.Api.Extensions;

public static class RateLimitingExtensions
{
    public static void AddAgroConnectRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter();
        builder.Services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.RejectionStatusCode = 429;
            var permits = configuration.GetValue("RateLimiting:PermitLimit", 120);
            var authPermits = configuration.GetValue("RateLimiting:AuthPermitLimit", 15);
            var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:WindowSeconds", 60));
            options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => Window(permits)));
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => Window(authPermits)));
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                await HttpExtensions.Error(429, "RATE_LIMITED", "Muitas requisições. Tente novamente em instantes.").ExecuteAsync(context.HttpContext);
            };
            FixedWindowRateLimiterOptions Window(int limit) => new()
            {
                PermitLimit = limit, Window = window, QueueLimit = 0, AutoReplenishment = true
            };
        });
    }
}
