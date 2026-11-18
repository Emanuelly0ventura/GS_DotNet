using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Api.Authentication;
using AgroConnect.Application.Security;
using AgroConnect.Domain.Entities;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AgroConnect.Domain.Rules;

namespace AgroConnect.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var autenticacao = api.MapGroup("/autenticacao").WithTags("Autenticação");
        autenticacao.MapPost("/cadastro", async (RegisterRequest input, AgroConnectDbContext db, JwtTokenService jwt, CancellationToken ct) =>
        {
            var details = new Dictionary<string, string>();
            var email = input.Email?.Trim().ToLowerInvariant() ?? "";
            if (!DomainRules.IsValidName(input.Name)) details["name"] = "Informe um nome de 2 a 150 caracteres.";
            if (!AccountRules.IsValidEmail(email))
                details["email"] = "Informe um e-mail válido.";
            if (input.Password?.Length is not (>= 8 and <= 128)) details["password"] = "Informe uma senha de 8 a 128 caracteres.";
            if (details.Count > 0) return HttpExtensions.Error(400, "VALIDATION_ERROR", "Os dados enviados são inválidos.", details);

            if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Duplicate();
            var user = new User { Name = input.Name!.Trim(), Email = email, PasswordHash = PasswordHasher.Hash(input.Password!) };
            db.Users.Add(user);
            try
            {
                return Results.Created("/api/v1/auth/me", await StartSession(user, db, jwt, ct));
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Duplicate();
                throw;
            }
        }).AllowAnonymous().RequireRateLimiting("auth").WithSummary("Cadastra um usuário")
          .Produces<AuthResponse>(201).Produces<ApiError>(409);

        var auth = api.MapGroup("/auth").WithTags("Autenticação");
        auth.MapPost("/login", async (LoginRequest input, AgroConnectDbContext db, JwtTokenService jwt, CancellationToken ct) =>
        {
            var email = input.Email?.Trim().ToLowerInvariant() ?? "";
            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
            if (user is null || input.Password?.Length is not (>= 8 and <= 128) || !PasswordHasher.Verify(input.Password, user.PasswordHash))
                return HttpExtensions.Error(401, "INVALID_CREDENTIALS", "E-mail ou senha inválidos.");

            var now = DateTimeOffset.UtcNow;
            await db.Sessions.Where(s => s.UserId == user.Id && s.ExpiresAt <= now).ExecuteDeleteAsync(ct);
            return Results.Ok(await StartSession(user, db, jwt, ct));
        }).AllowAnonymous().RequireRateLimiting("auth").WithSummary("Autentica e retorna JWT")
          .Produces<AuthResponse>().Produces<ApiError>(401);

        auth.MapGet("/me", async (HttpContext ctx, AgroConnectDbContext db) =>
        {
            var id = ctx.User.UserId();
            var user = await db.Users.FindAsync([id], ctx.RequestAborted);
            return user is null ? HttpExtensions.Error(404, "NOT_FOUND", "Usuário não encontrado.") : Results.Ok(ToResponse(user));
        }).RequireAuthorization().WithSummary("Retorna o usuário autenticado").Produces<UserResponse>().Produces<ApiError>(404);

        autenticacao.MapPost("/sair", async (HttpContext ctx, AgroConnectDbContext db) =>
        {
            var sessionId = ctx.User.SessionId();
            var userId = ctx.User.UserId();
            await db.Sessions.Where(s => s.Id == sessionId && s.UserId == userId).ExecuteDeleteAsync(ctx.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization().WithSummary("Revoga a sessão e remove seu token de push").Produces(204);
        return api;
    }

    private static async Task<AuthResponse> StartSession(User user, AgroConnectDbContext db, JwtTokenService jwt, CancellationToken ct)
    {
        var session = new Session { UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(JwtTokenService.ExpiresIn) };
        db.Sessions.Add(session);
        var token = jwt.Create(user, session);
        await db.SaveChangesAsync(ct);
        return new AuthResponse(token, "Bearer", JwtTokenService.ExpiresIn, ToResponse(user));
    }

    private static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Email, user.CreatedAt);
    private static IResult Duplicate() => HttpExtensions.Error(409, "EMAIL_ALREADY_EXISTS", "Este e-mail já está cadastrado.");
}
