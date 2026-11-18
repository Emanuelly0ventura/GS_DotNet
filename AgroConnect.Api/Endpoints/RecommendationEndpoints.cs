using AgroConnect.Application.Pagination;
using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static RouteGroupBuilder MapRecommendationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/recomendacoes", async (RecommendationInput input, HttpContext ctx, AgroConnectDbContext db) =>
        {
            if (input.DeviceId == Guid.Empty)
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe o dispositivo.");
            var userId = ctx.User.UserId();
            var ct = ctx.RequestAborted;
            if (!await db.Devices.AnyAsync(d => d.Id == input.DeviceId && d.Field!.Property!.UserId == userId, ct))
                return HttpExtensions.Error(404, "NOT_FOUND", "Dispositivo não encontrado.");

            var alerts = await db.Alerts.AsNoTracking().Where(a => a.DeviceId == input.DeviceId && a.Status != "RESOLVED")
                .OrderByDescending(a => a.Severity == "CRITICAL").ThenByDescending(a => a.Severity == "WARNING")
                .ThenByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Take(20).ToListAsync(ct);
            var items = alerts.Select(AlertEndpoints.ToResponse).Where(a => a.BasedOn.Any(r => r.Value.HasValue))
                .Select(a => new RecommendationResponse(a.Id, a.CreatedAt, a.Severity, a.Title, a.Message, a.Reason,
                    a.BasedOn, "Recomendação informativa baseada em dados simulados. Avalie as condições reais antes de decidir.", true)).ToArray();
            return Results.Ok(PageBuilder.ToPage(items, 1, 20, items.Length));
        }).RequireAuthorization().WithTags("Recomendações de demonstração")
            .WithSummary("Retorna orientações informativas baseadas nos alertas simulados do dispositivo")
            .Produces<PageResponse<RecommendationResponse>>().Produces<ApiError>(404);
        return api;
    }
}
