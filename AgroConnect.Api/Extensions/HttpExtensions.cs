using System.Security.Claims;
using AgroConnect.Application.DTOs;
using AgroConnect.Api.Filters;

namespace AgroConnect.Api.Extensions;

public static class HttpExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id)
            ? id : throw new UnauthorizedAccessException();

    public static Guid SessionId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("jti")!);

    public static IResult Error(int status, string code, string message, object? details = null) =>
        Results.Json(new ApiError(code, message, details, DateTimeOffset.UtcNow), statusCode: status);

    public static RouteHandlerBuilder WithIdempotency<TInput, TResponse>(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new IdempotentAttribute()).AddEndpointFilter((invocation, next) =>
            invocation.HttpContext.RequestServices.GetRequiredService<IdempotencyService>().ExecuteAsync<TInput, TResponse>(invocation, next));
}
