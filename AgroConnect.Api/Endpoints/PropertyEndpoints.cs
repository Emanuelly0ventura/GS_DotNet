using AgroConnect.Api.Extensions;
using AgroConnect.Application.DTOs;
using AgroConnect.Application.Services.Interfaces;

namespace AgroConnect.Api.Endpoints;

public static class PropertyEndpoints
{
    public static RouteGroupBuilder MapPropertyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/properties").RequireAuthorization().WithTags("Propriedades");
        group.MapGet("/", async (HttpContext ctx, IPropertyService properties, int page = 1, int size = 20) =>
            Results.Ok(await properties.ListAsync(ctx.User.UserId(), page, size, ctx.RequestAborted)))
            .WithSummary("Lista propriedades do usuário").Produces<PageResponse<PropertyResponse>>();

        group.MapPost("/", (PropertyInput input, HttpContext ctx, IPropertyService properties) =>
        {
            var result = properties.PrepareCreation(ctx.User.UserId(), input);
            return result.Property is { } property
                ? (IResult)TypedResults.Created($"/api/v1/properties/{property.Id}", property)
                : Invalid(result.Errors);
        }).WithIdempotency<PropertyInput, PropertyResponse>().WithSummary("Cria propriedade").Produces<PropertyResponse>(201);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IPropertyService properties) =>
        {
            var property = await properties.GetAsync(ctx.User.UserId(), id, ctx.RequestAborted);
            return property is null ? Missing() : Results.Ok(property);
        }).WithSummary("Obtém uma propriedade").Produces<PropertyResponse>().Produces<ApiError>(404);

        group.MapPatch("/{id:guid}", async (Guid id, PropertyPatch input, HttpContext ctx, IPropertyService properties) =>
        {
            var result = await properties.UpdateAsync(ctx.User.UserId(), id, input, ctx.RequestAborted);
            if (result is null) return Missing();
            return result.Property is { } property ? Results.Ok(property) : Invalid(result.Errors);
        }).WithSummary("Atualiza os campos enviados da propriedade").Produces<PropertyResponse>().Produces<ApiError>(404);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IPropertyService properties) =>
            await properties.DeleteAsync(ctx.User.UserId(), id, ctx.RequestAborted) ? Results.NoContent() : Missing())
            .WithSummary("Exclui propriedade e seus dados vinculados").Produces(204).Produces<ApiError>(404);
        return api;
    }

    private static IResult Missing() => HttpExtensions.Error(404, "NOT_FOUND", "Propriedade não encontrada.");

    private static IResult Invalid(IReadOnlyDictionary<string, string> errors) =>
        HttpExtensions.Error(400, "VALIDATION_ERROR", "Os dados enviados são inválidos.", errors);
}
