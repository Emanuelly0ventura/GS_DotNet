using AgroConnect.Application.Pagination;
using AgroConnect.Application.DTOs;
using AgroConnect.Api.Extensions;
using AgroConnect.Domain.Entities;
using AgroConnect.Domain.Rules;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Endpoints;

public static class FieldEndpoints
{
    public static RouteGroupBuilder MapFieldEndpoints(this RouteGroupBuilder api)
    {
        var properties = api.MapGroup("/propriedades/{propertyId:guid}/talhoes").RequireAuthorization().WithTags("Talhões");
        properties.MapGet("/", async (Guid propertyId, HttpContext ctx, AgroConnectDbContext db, int page = 1, int size = 20) =>
        {
            if (!await OwnsProperty(propertyId, ctx, db)) return Missing("Propriedade não encontrada.");
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);
            var query = db.Fields.AsNoTracking().Where(f => f.PropertyId == propertyId).OrderBy(f => f.Name).ThenBy(f => f.Id);
            var total = await query.CountAsync(ctx.RequestAborted);
            var items = await query.Skip(PageBuilder.Offset(page, size)).Take(size)
                .Select(f => new FieldResponse(f.Id, f.PropertyId, f.Name, f.AreaHectares)).ToListAsync(ctx.RequestAborted);
            return Results.Ok(PageBuilder.ToPage(items, page, size, total));
        }).WithSummary("Lista talhões da propriedade").Produces<PageResponse<FieldResponse>>().Produces<ApiError>(404);

        properties.MapPost("/", async (Guid propertyId, FieldInput input, HttpContext ctx, AgroConnectDbContext db) =>
        {
            if (!DomainRules.IsValidName(input.Name) || !DomainRules.IsValidArea(input.AreaHectares))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe nome válido e área maior que zero.");
            if (!await OwnsProperty(propertyId, ctx, db)) return Missing("Propriedade não encontrada.");
            var field = new Field { PropertyId = propertyId, Name = input.Name.Trim(), AreaHectares = input.AreaHectares };
            db.Fields.Add(field);
            return TypedResults.Created($"/api/v1/talhoes/{field.Id}",
                new FieldResponse(field.Id, field.PropertyId, field.Name, field.AreaHectares));
        }).WithIdempotency<FieldInput, FieldResponse>().WithSummary("Cria talhão").Produces<FieldResponse>(201).Produces<ApiError>(404);

        var fields = api.MapGroup("/talhoes").RequireAuthorization().WithTags("Talhões e plantios");
        fields.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var userId = ctx.User.UserId();
            var field = await db.Fields.AsNoTracking().Where(f => f.Id == id && f.Property!.UserId == userId)
                .Select(f => new FieldResponse(f.Id, f.PropertyId, f.Name, f.AreaHectares)).SingleOrDefaultAsync(ctx.RequestAborted);
            return field is null ? Missing("Talhão não encontrado.") : Results.Ok(field);
        }).WithSummary("Obtém um talhão").Produces<FieldResponse>().Produces<ApiError>(404);

        fields.MapGet("/{id:guid}/plantios", async (Guid id, HttpContext ctx, AgroConnectDbContext db, int page = 1, int size = 20) =>
        {
            if (!await OwnsField(id, ctx, db)) return Missing("Talhão não encontrado.");
            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);
            var query = db.Plantings.AsNoTracking().Where(p => p.FieldId == id).OrderByDescending(p => p.PlantedAt).ThenBy(p => p.Id);
            var total = await query.CountAsync(ctx.RequestAborted);
            var items = await query.Skip(PageBuilder.Offset(page, size)).Take(size)
                .Select(p => new PlantingResponse(p.Id, p.FieldId, p.CropName, p.PlantedAt, p.Stage)).ToListAsync(ctx.RequestAborted);
            return Results.Ok(PageBuilder.ToPage(items, page, size, total));
        }).WithSummary("Lista plantios do talhão").Produces<PageResponse<PlantingResponse>>().Produces<ApiError>(404);

        fields.MapGet("/{id:guid}/plantios/{plantingId:guid}", async (Guid id, Guid plantingId, HttpContext ctx, AgroConnectDbContext db) =>
        {
            var userId = ctx.User.UserId();
            var planting = await db.Plantings.AsNoTracking().Where(p => p.Id == plantingId && p.FieldId == id && p.Field!.Property!.UserId == userId)
                .Select(p => new PlantingResponse(p.Id, p.FieldId, p.CropName, p.PlantedAt, p.Stage)).SingleOrDefaultAsync(ctx.RequestAborted);
            return planting is null ? Missing("Plantio não encontrado.") : Results.Ok(planting);
        }).WithSummary("Obtém um plantio").Produces<PlantingResponse>().Produces<ApiError>(404);

        fields.MapPost("/{id:guid}/plantios", async (Guid id, PlantingInput input, HttpContext ctx, AgroConnectDbContext db) =>
        {
            if (input.CropName?.Trim().Length is not (>= 2 and <= 150) || input.PlantedAt == default || !DomainRules.IsValidPlantingStage(input.Stage))
                return HttpExtensions.Error(400, "VALIDATION_ERROR", "Informe cultura, data e estágio válidos.");
            if (!await OwnsField(id, ctx, db)) return Missing("Talhão não encontrado.");
            var planting = new Planting { FieldId = id, CropName = input.CropName.Trim(), PlantedAt = input.PlantedAt, Stage = input.Stage.ToUpperInvariant() };
            db.Plantings.Add(planting);
            return TypedResults.Created($"/api/v1/talhoes/{id}/plantios/{planting.Id}",
                new PlantingResponse(planting.Id, planting.FieldId, planting.CropName, planting.PlantedAt, planting.Stage));
        }).WithIdempotency<PlantingInput, PlantingResponse>().WithSummary("Registra um plantio").Produces<PlantingResponse>(201).Produces<ApiError>(404);
        return api;
    }

    private static Task<bool> OwnsProperty(Guid id, HttpContext ctx, AgroConnectDbContext db)
    {
        var userId = ctx.User.UserId();
        return db.Properties.AnyAsync(p => p.Id == id && p.UserId == userId, ctx.RequestAborted);
    }

    private static Task<bool> OwnsField(Guid id, HttpContext ctx, AgroConnectDbContext db)
    {
        var userId = ctx.User.UserId();
        return db.Fields.AnyAsync(f => f.Id == id && f.Property!.UserId == userId, ctx.RequestAborted);
    }

    private static IResult Missing(string message) => HttpExtensions.Error(404, "NOT_FOUND", message);
}
