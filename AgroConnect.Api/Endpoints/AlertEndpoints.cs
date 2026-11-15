using AgroConnect.Application.Pagination;
using System.Text.Json;
using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Domain.Entities;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Endpoints;

public static class AlertEndpoints
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapAlertEndpoints(this RouteGroupBuilder api)
    {
        var alerts = api.MapGroup("/alerts").RequireAuthorization().WithTags("Alertas");
        alerts.MapGet("/", async (HttpContext ctx, AgroConnectDbContext db, string? status, int page = 1, int size = 20) =>
        {
            var normalized = status?.Trim().ToUpperInvariant();
            if (normalized is not (null or "UNREAD" or "READ" or "RESOLVED"))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe status UNREAD, READ ou RESOLVED.");
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);
            var query = OwnedAlerts(db, ctx.User.UserId()).AsNoTracking();
            if (normalized is not null) query = query.Where(a => a.Status == normalized);
            var ct = ctx.RequestAborted;
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(a => a.Severity == "CRITICAL")
                .ThenByDescending(a => a.Severity == "WARNING").ThenByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
                .Skip(PageBuilder.Offset(page, size)).Take(size).ToListAsync(ct);
            return Results.Ok(PageBuilder.ToPage(items.Select(ToResponse).ToArray(), page, size, total));
        }).WithSummary("Lista alertas por severidade e data").Produces<PageResponse<AlertResponse>>();


        alerts.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var alert = await OwnedAlerts(db, ctx.User.UserId()).AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == id, ctx.RequestAborted);
            return alert is null ? HttpExtensions.Error(404, "NOT_FOUND", "Alerta não encontrado.") : Results.Ok(ToResponse(alert));
        }).WithSummary("Obtém um alerta do usuário").Produces<AlertResponse>().Produces<ApiError>(404);

        alerts.MapPost("/", async (AlertInput input, HttpContext ctx, AgroConnectDbContext db, IConfiguration config) =>
        {
            var severity = input.Severity?.Trim().ToUpperInvariant();
            if (input.DeviceId == Guid.Empty || severity is not ("INFO" or "WARNING" or "CRITICAL") ||
                input.Title?.Trim().Length is not (>= 3 and <= 120) || input.Message?.Trim().Length is not (>= 3 and <= 500) ||
                input.Reason?.Trim().Length is not (>= 3 and <= 500))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe dispositivo, severidade, título, mensagem e motivo válidos.");

            var userId = ctx.User.UserId();
            var ct = ctx.RequestAborted;
            if (!await db.Devices.AnyAsync(d => d.Id == input.DeviceId && d.Field!.Property!.UserId == userId, ct))
                return HttpExtensions.Error(404, "NOT_FOUND", "Dispositivo não encontrado.");

            var evidence = await db.Readings.AsNoTracking()
                .Where(r => r.Sensor!.DeviceId == input.DeviceId && r.Quality == "VALID" && r.Value != null &&
                    r.Id == db.Readings.Where(other => other.SensorId == r.SensorId && other.Quality == "VALID" && other.Value != null)
                        .OrderByDescending(other => other.RecordedAt).ThenBy(other => other.Id).Select(other => other.Id).First())
                .OrderBy(r => r.Sensor!.Type).Take(10)
                .Select(r => new ReadingSnapshot(r.Sensor!.Type, r.Value, r.Sensor.Unit, r.RecordedAt)).ToArrayAsync(ct);

            var alert = new Alert
            {
                DeviceId = input.DeviceId, Severity = severity, Title = input.Title.Trim(), Message = input.Message.Trim(),
                Reason = input.Reason.Trim(), BasedOnJson = JsonSerializer.Serialize(evidence, SnapshotOptions),
                PushPending = config.GetValue("Push:Enabled", false)
            };
            db.Alerts.Add(alert);
            return TypedResults.Created($"/api/v1/alerts/{alert.Id}", ToResponse(alert));
        }).WithIdempotency<AlertInput, AlertResponse>().WithSummary("Registra alerta para um dispositivo do usuário")
          .Produces<AlertResponse>(201).Produces<ApiError>(404);

        alerts.MapPatch("/{id:guid}/read", async (Guid id, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var query = OwnedAlerts(db, ctx.User.UserId()).Where(a => a.Id == id);
            var ct = ctx.RequestAborted;
            await query.Where(a => a.Status == "UNREAD").ExecuteUpdateAsync(change => change.SetProperty(a => a.Status, "READ"), ct);
            var alert = await query.AsNoTracking().SingleOrDefaultAsync(ct);
            return alert is null ? HttpExtensions.Error(404, "NOT_FOUND", "Alerta não encontrado.") : Results.Ok(ToResponse(alert));
        }).WithSummary("Marca alerta como lido").Produces<AlertResponse>().Produces<ApiError>(404);
        return api;
    }

    private static IQueryable<Alert> OwnedAlerts(AgroConnectDbContext db, Guid userId) =>
        db.Alerts.Where(a => a.Device!.Field!.Property!.UserId == userId);

    internal static AlertResponse ToResponse(Alert a) => new(a.Id, a.DeviceId, a.Severity, a.Status,
        a.Title, a.Message, a.Reason, JsonSerializer.Deserialize<ReadingSnapshot[]>(a.BasedOnJson, SnapshotOptions) ?? [], a.CreatedAt);
}
