using System.Security.Cryptography;
using System.Text;
using AgroConnect.Api.Authentication;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AgroConnect.Api.Extensions;

public static class AuthenticationExtensions
{
    public static void AddAgroConnectAuthentication(this WebApplicationBuilder builder)
    {
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (jwtKey is null && builder.Environment.IsDevelopment())
            builder.Configuration["Jwt:Key"] = jwtKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Configure Jwt__Key com no mínimo 32 bytes.");

        builder.Services.AddScoped<JwtTokenService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AgroConnect",
                ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AgroConnect.Mobile",
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<AgroConnectDbContext>();
                    var principal = context.Principal!;
                    if (!Guid.TryParse(principal.FindFirst("jti")?.Value, out var sessionId) ||
                        !Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) ||
                        !await db.Sessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, context.HttpContext.RequestAborted))
                        context.Fail("Sessão encerrada.");
                }
            };
        });
        builder.Services.AddAuthorization();
    }
}
