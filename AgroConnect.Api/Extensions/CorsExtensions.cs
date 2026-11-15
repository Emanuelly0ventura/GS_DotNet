using Microsoft.AspNetCore.Cors.Infrastructure;

namespace AgroConnect.Api.Extensions;

public static class CorsExtensions
{
    public static void AddAgroConnectCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
            if (builder.Environment.IsDevelopment()) policy.AllowAnyOrigin();
            else policy.WithOrigins(origins);
            policy.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Location", "Retry-After");
        }));
    }
}
