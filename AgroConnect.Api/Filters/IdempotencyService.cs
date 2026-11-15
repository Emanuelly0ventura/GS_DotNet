using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgroConnect.Api.Extensions;
using AgroConnect.Domain.Entities;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgroConnect.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute;

public sealed class IdempotencyService(AgroConnectDbContext db, IOptions<JsonOptions> json)
{
    public async ValueTask<object?> ExecuteAsync<TInput, TResponse>(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        var ct = context.RequestAborted;
        var userId = context.User.UserId();
        var header = context.Request.Headers["Idempotency-Key"];
        if (header.Count > 1 || (header.Count == 1 && (string.IsNullOrWhiteSpace(header[0]) || header[0]!.Length > 128)))
            return HttpExtensions.Error(400, "INVALID_IDEMPOTENCY_KEY", "Informe uma chave de idempotência de até 128 caracteres.");

        var body = invocation.Arguments.OfType<TInput>().Single();
        var path = context.Request.Path.Value!.TrimEnd('/').ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path + "\n" + JsonSerializer.Serialize(body, json.Value.SerializerOptions))));
        var automatic = header.Count == 0;
        var key = automatic ? $"auto:{hash}" : $"client:{header[0]}";
        var record = await Find();
        var now = DateTimeOffset.UtcNow;
        if (record is not null && (!automatic || now - record.CreatedAt < TimeSpan.FromMinutes(5)))
            return Replay(record);

        var result = await next(invocation);
        if (result is not (Created<TResponse> created))
            return result;

        if (record is null)
        {
            record = new IdempotencyRecord { UserId = userId, Key = key };
            db.IdempotencyRecords.Add(record);
        }
        record.RequestHash = hash;
        record.StatusCode = 201;
        record.ResponseJson = JsonSerializer.Serialize(created.Value, json.Value.SerializerOptions);
        record.Location = created.Location!;
        record.CreatedAt = now;
        try
        {
            // The domain insert and replay record commit in the same SaveChanges transaction.
            await db.SaveChangesAsync(ct);
            return result;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var winner = await Find();
            if (winner is null || (automatic && now - winner.CreatedAt >= TimeSpan.FromMinutes(5)))
                throw;
            return Replay(winner);
        }

        Task<IdempotencyRecord?> Find() => db.IdempotencyRecords.SingleOrDefaultAsync(x => x.UserId == userId && x.Key == key, ct);

        IResult Replay(IdempotencyRecord prior)
        {
            if (prior.RequestHash != hash)
                return HttpExtensions.Error(409, "IDEMPOTENCY_CONFLICT", "A chave já foi utilizada com outro conteúdo.");
            context.Response.Headers.Location = prior.Location;
            return Results.Content(prior.ResponseJson, "application/json", statusCode: prior.StatusCode);
        }
    }
}
