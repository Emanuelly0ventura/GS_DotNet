using AgroConnect.Application.Pagination;
using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Domain.Entities;
using AgroConnect.Domain.Rules;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder api)
    {
        var devices = api.MapGroup("/dispositivos").RequireAuthorization().WithTags("Dispositivos e leituras");
        devices.MapGet("/", async (HttpContext ctx, AgroConnectDbContext db, Guid? fieldId = null, int page = 1, int size = 20) =>
        {
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);
            var query = OwnedDevices(db, ctx.User.UserId());
            if (fieldId.HasValue) query = query.Where(d => d.FieldId == fieldId);
            var ct = ctx.RequestAborted;
            var total = await query.CountAsync(ct);
            var rows = await query.OrderBy(d => d.Code).ThenBy(d => d.Id).Skip(PageBuilder.Offset(page, size)).Take(size).ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(PageBuilder.ToPage(rows.Select(d => ToResponse(d, now)).ToArray(), page, size, total));
        }).WithSummary("Lista dispositivos do usuário").Produces<PageResponse<DeviceResponse>>();

        devices.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var device = await OwnedDevices(db, ctx.User.UserId()).SingleOrDefaultAsync(d => d.Id == id, ctx.RequestAborted);
            return device is null ? MissingDevice() : Results.Ok(ToResponse(device, DateTimeOffset.UtcNow));
        }).WithSummary("Obtém dispositivo").Produces<DeviceResponse>().Produces<ApiError>(404);

        devices.MapGet("/{id:guid}/sensores", async (Guid id, HttpContext ctx, AgroConnectDbContext db, int page = 1, int size = 20) =>
        {
            var ct = ctx.RequestAborted;
            if (!await OwnedDevices(db, ctx.User.UserId()).AnyAsync(d => d.Id == id, ct)) return MissingDevice();
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);
            var query = db.Sensors.AsNoTracking().Where(s => s.DeviceId == id).OrderBy(s => s.Type).ThenBy(s => s.Id);
            var total = await query.CountAsync(ct);
            var items = await query.Skip(PageBuilder.Offset(page, size)).Take(size)
                .Select(s => new SensorResponse(s.Id, s.DeviceId, s.Type, s.Label, s.Unit)).ToListAsync(ct);
            return Results.Ok(PageBuilder.ToPage(items, page, size, total));
        }).WithSummary("Lista sensores").Produces<PageResponse<SensorResponse>>().Produces<ApiError>(404);

        devices.MapGet("/{id:guid}/leituras/ultimas", async (Guid id, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var ct = ctx.RequestAborted;
            if (!await OwnedDevices(db, ctx.User.UserId()).AnyAsync(d => d.Id == id, ct)) return MissingDevice();
            var readings = await db.Readings.AsNoTracking().Where(r => r.Sensor!.DeviceId == id &&
                    r.Id == db.Readings.Where(other => other.SensorId == r.SensorId)
                        .OrderByDescending(other => other.RecordedAt).ThenBy(other => other.Id).Select(other => other.Id).First())
                .OrderBy(r => r.Sensor!.Type).Select(r => new ReadingResponse(r.SensorId, r.Sensor!.Type,
                    r.Quality == "VALID" ? r.Value : null, r.Sensor.Unit, r.Quality, r.RecordedAt)).ToListAsync(ct);
            return Results.Ok(new LatestReadingsResponse(id, readings.Select(r => (DateTimeOffset?)r.RecordedAt).Max(), readings, true));
        }).WithSummary("Retorna a última leitura de cada sensor com dados").Produces<LatestReadingsResponse>().Produces<ApiError>(404);

        devices.MapGet("/{id:guid}/leituras", async (Guid id, string? type, Guid? sensorId,
            DateTimeOffset? from, DateTimeOffset? to, string? interval, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var ct = ctx.RequestAborted;
            if (!await OwnedDevices(db, ctx.User.UserId()).AnyAsync(d => d.Id == id, ct)) return MissingDevice();
            if (sensorId is null && string.IsNullOrWhiteSpace(type))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe type ou sensorId.");
            interval ??= "raw";
            var minutes = interval switch { "raw" => 0, "5m" => 5, "15m" => 15, "1h" => 60, "1d" => 1440, _ => -1 };
            var end = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var start = (from ?? (end < DateTimeOffset.MinValue.AddHours(24) ? DateTimeOffset.MinValue : end.AddHours(-24))).ToUniversalTime();
            if (minutes < 0 || start >= end || end - start > TimeSpan.FromDays(366))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Use intervalo raw, 5m, 15m, 1h ou 1d e um período de até 366 dias.");
            var sensors = db.Sensors.AsNoTracking().Where(s => s.DeviceId == id);
            if (sensorId.HasValue) sensors = sensors.Where(s => s.Id == sensorId);
            if (!string.IsNullOrWhiteSpace(type)) sensors = sensors.Where(s => s.Type == type);
            var sensor = await sensors.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
            if (sensor is null) return HttpExtensions.Error(404, "NOT_FOUND", "Sensor não encontrado neste dispositivo.");

            var rows = await db.Readings.AsNoTracking()
                .Where(r => r.SensorId == sensor.Id && r.RecordedAt >= start && r.RecordedAt <= end)
                .OrderBy(r => r.RecordedAt).ThenBy(r => r.Id).Take(50001).ToListAsync(ct);
            if (rows.Count > 50000)
                return HttpExtensions.Error(422, "HISTORY_TOO_LARGE", "Use um período menor para consultar o histórico.");

            var items = minutes == 0
                ? rows.Select(r => ToPoint(r.RecordedAt, [r])).ToArray()
                : rows.GroupBy(r => DomainRules.BucketStart(r.RecordedAt, minutes)).Select(g => ToPoint(g.Key, g)).ToArray();
            var valid = rows.Where(r => r.Quality == "VALID" && r.Value.HasValue).Select(r => r.Value!.Value).ToArray();
            var summary = valid.Length == 0 ? null : new HistorySummary(valid.Min(), valid.Max(), valid.Average());
            return Results.Ok(new HistoryResponse(id, sensor.Id, sensor.Type, sensor.Unit, interval, items, summary, true));
        }).WithSummary("Consulta e agrega o histórico de um sensor").Produces<HistoryResponse>().Produces<ApiError>(404).Produces<ApiError>(422);
        return api;
    }

    private static IQueryable<Device> OwnedDevices(AgroConnectDbContext db, Guid userId) =>
        db.Devices.AsNoTracking().Where(d => d.Field!.Property!.UserId == userId);

    private static DeviceResponse ToResponse(Device d, DateTimeOffset now) =>
        new(d.Id, d.Code, d.Name, d.FieldId, DomainRules.DeviceStatus(d.LastSeenAt, now), d.LastSeenAt, d.Rssi, d.FirmwareVersion);

    private static HistoryPoint ToPoint(DateTimeOffset timestamp, IEnumerable<Reading> readings)
    {
        var rows = readings.ToArray();
        var values = rows.Where(r => r.Quality == "VALID").Select(r => r.Value).ToArray();
        if (values.Any(v => v.HasValue))
            return new HistoryPoint(timestamp, values.Average(), values.Min(), values.Max(), rows.Length, "VALID");
        var quality = rows.FirstOrDefault(r => r.Quality != "VALID")?.Quality ?? "SENSOR_ERROR";
        return new HistoryPoint(timestamp, null, null, null, rows.Length, quality);
    }

    private static IResult MissingDevice() => HttpExtensions.Error(404, "NOT_FOUND", "Dispositivo não encontrado.");
}
