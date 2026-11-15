using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgroConnect.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace AgroConnect.Api.Authentication;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public const int ExpiresIn = 3600;

    public string Create(User user, Session session)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "AgroConnect";
        var audience = configuration["Jwt:Audience"] ?? "AgroConnect.Mobile";
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurada.");
        Claim[] claims = [new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(JwtRegisteredClaimNames.Jti, session.Id.ToString())];
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: session.ExpiresAt.UtcDateTime, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
