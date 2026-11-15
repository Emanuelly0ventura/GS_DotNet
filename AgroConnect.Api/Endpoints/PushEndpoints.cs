using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Domain.Entities;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Endpoints;

public static class PushEndpoints
{
    public static RouteGroupBuilder MapPushEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/me/push-tokens", async (PushTokenInput input, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var token = input.Token?.Trim();
            var platform = input.Platform?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 512 || !IsExpoToken(token) || platform is not ("android" or "ios"))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe um token de push e uma plataforma válidos.");

            var userId = ctx.User.UserId();
            var sessionId = ctx.User.SessionId();
            var ct = ctx.RequestAborted;
            // A device token has a single owner, even when a different account logs in on that device.
            if (await UpdateAsync() == 0)
            {
                db.PushTokens.Add(new PushToken { UserId = userId, SessionId = sessionId, Token = token, Platform = platform });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    if (await UpdateAsync() == 0) throw;
                }
            }
            return Results.NoContent();

            Task<int> UpdateAsync() => db.PushTokens.Where(x => x.Token == token).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.UserId, userId).SetProperty(x => x.SessionId, sessionId)
                .SetProperty(x => x.Platform, platform), ct);
        }).RequireAuthorization().WithTags("Notificações").WithSummary("Registra o token Expo do aparelho").Produces(204);
        return api;
    }
    private static bool IsExpoToken(string token) =>
        (token.StartsWith("ExpoPushToken[", StringComparison.Ordinal) || token.StartsWith("ExponentPushToken[", StringComparison.Ordinal)) &&
        token.EndsWith(']') && token[(token.IndexOf('[') + 1)..^1] is { Length: > 0 } value &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
