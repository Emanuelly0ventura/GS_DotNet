using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroConnect.Application.DTOs;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroConnect.Tests.Integration;
using static AgroConnect.Tests.Integration.MobileContractIntegrationTests;

public sealed class ExpoPushIntegrationTests
{
    private const string Root = "/api/v1";

    [Fact]
    public async Task Alert_creation_queues_one_expo_notification_and_replays_the_result()
    {
        var expo = new ExpoStub();
        await using var factory = new ApiFactory(seed: true, expo: expo);
        using var client = factory.CreateClient();
        await Login(client, "demo@agroconnect.com");
        var devices = (await client.GetFromJsonAsync<PageResponse<DeviceResponse>>(Root + "/devices"))!.Items;
        var device = Assert.Single(devices, d => d.Status == "ONLINE");
        var push = new PushTokenInput("ExponentPushToken[test]", "android");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(Root + "/me/push-tokens", push)).StatusCode);

        var alert = new AlertInput(device.Id, "INFO", "Alerta de teste", "Consulte as medições", "Validação de integração");
        var path = Root + "/alertas";
        var first = await PostWithKey(client, path, alert, "alert-1");
        var repeated = await PostWithKey(client, path, alert, "alert-1");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(first.Headers.Location, repeated.Headers.Location);
        var created = (await first.Content.ReadFromJsonAsync<AlertResponse>())!;
        Assert.NotEmpty(created.BasedOn);
        Assert.Equal(created.Id, (await client.GetFromJsonAsync<AlertResponse>(first.Headers.Location))!.Id);

        var payload = await expo.Received.Task.WaitAsync(TimeSpan.FromSeconds(12));
        Assert.Equal(push.Token, payload.GetProperty("to").GetString());
        Assert.Equal("alerts", payload.GetProperty("channelId").GetString());
        Assert.Equal(created.Id, payload.GetProperty("data").GetProperty("alertId").GetGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroConnectDbContext>();
        Assert.Equal(2, await db.Alerts.CountAsync());
        Assert.False(await db.Alerts.AnyAsync(a => a.PushPending));
    }
}

internal sealed class ExpoStub : HttpMessageHandler
{
    public TaskCompletionSource<JsonElement> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Received.SetResult(await request.Content!.ReadFromJsonAsync<JsonElement>(ct));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new { status = "ok", id = "test-ticket" } }) };
    }
}
