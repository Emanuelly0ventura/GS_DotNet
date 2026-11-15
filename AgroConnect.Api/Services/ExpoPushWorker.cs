using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Services;

public sealed class ExpoPushWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, IConfiguration config,
    ILogger<ExpoPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Push:Enabled", false)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao consultar a fila de push."); }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroConnectDbContext>();
        var pending = await db.Alerts.AsNoTracking().Where(a => a.PushPending)
            .OrderBy(a => a.CreatedAt).Take(20)
            .Select(a => new { a.Id, UserId = a.Device!.Field!.Property!.UserId }).ToListAsync(ct);

        foreach (var alert in pending)
        {
            var now = DateTimeOffset.UtcNow;
            var recipients = await db.PushTokens.AsNoTracking()
                .Where(p => p.UserId == alert.UserId && db.Sessions.Any(s => s.Id == p.SessionId && s.ExpiresAt > now))
                .Select(p => new { p.Id, p.Token, p.SessionId }).ToListAsync(ct);

            // Claim once before external I/O, since Expo may accept a message even when the response times out.
            var claimed = await db.Alerts.Where(a => a.Id == alert.Id && a.PushPending)
                .ExecuteUpdateAsync(update => update.SetProperty(a => a.PushPending, false), ct);
            if (claimed == 0) continue;

            foreach (var recipient in recipients)
            {
                if (!await db.PushTokens.AnyAsync(p => p.Id == recipient.Id && p.SessionId == recipient.SessionId && p.UserId == alert.UserId, ct))
                    continue;
                try
                {
                    if (await SendAsync(recipient.Token, alert.Id, ct))
                        await db.PushTokens.Where(p => p.Id == recipient.Id && p.SessionId == recipient.SessionId).ExecuteDeleteAsync(ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning("Falha no envio de push para o alerta {AlertId} ({ErrorType}).", alert.Id, ex.GetType().Name);
                }
            }
        }
    }

    private async Task<bool> SendAsync(string token, Guid alertId, CancellationToken ct)
    {
        using var client = clients.CreateClient("expo-push");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://exp.host/--/api/v2/push/send")
        {
            Content = JsonContent.Create(new
            {
                to = token,
                title = "Novo alerta no AgroConnect",
                body = "Abra o app para consultar os detalhes.",
                channelId = "alerts",
                data = new { screen = "alerts", alertId }
            })
        };
        if (config["Push:AccessToken"] is { Length: > 0 } secret)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var ticket) &&
            ticket.ValueKind == JsonValueKind.Object && ticket.TryGetProperty("status", out var status))
        {
            if (status.GetString() == "ok") return false;
            if (ticket.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) &&
                error.GetString() == "DeviceNotRegistered") return true;
        }
        logger.LogWarning("Expo recusou o push para o alerta {AlertId}.", alertId);
        return false;
    }
}

