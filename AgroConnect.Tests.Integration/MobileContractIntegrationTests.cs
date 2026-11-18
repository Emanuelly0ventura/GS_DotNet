using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgroConnect.Application.DTOs;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroConnect.Tests.Integration;

public sealed class MobileContractIntegrationTests
{
    private const string Root = "/api/v1";

    [Fact]
    public async Task Registration_login_logout_and_push_follow_mobile_contract()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Root + "/auth/me")).StatusCode);
        var bad = await client.PostAsJsonAsync(Root + "/auth/register", new { name = "A", email = "invalido" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("details").TryGetProperty("password", out _));

        var internalEmail = await client.PostAsJsonAsync(Root + "/auth/register",
            new RegisterRequest("Ana", "ana@localhost", ApiFactory.Password));
        Assert.Equal(HttpStatusCode.BadRequest, internalEmail.StatusCode);
        Assert.True((await internalEmail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("details").TryGetProperty("email", out _));

        var email = "ana@example.test";
        var auth = await Register(client, email);
        var duplicate = await client.PostAsJsonAsync(Root + "/auth/register", new RegisterRequest("Outra", "ANA@example.test", ApiFactory.Password));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var wrong = await client.PostAsJsonAsync(Root + "/auth/login", new LoginRequest(email, "invalid-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var login = await Login(client, email);
        Assert.NotEqual(auth.AccessToken, login.AccessToken);

        var user = await client.GetFromJsonAsync<UserResponse>(Root + "/auth/me");
        Assert.Equal(auth.User.Id, user!.Id);
        var push = new PushTokenInput("ExponentPushToken[test]", "android");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(Root + "/me/push-tokens", push)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(Root + "/me/push-tokens", push)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroConnectDbContext>();
        Assert.Equal(1, await db.PushTokens.CountAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Root + "/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Root + "/auth/me")).StatusCode);
        Assert.Empty(await db.PushTokens.ToListAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Root + "/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Property_creation_replays_concurrent_calls_and_hides_resources_from_other_users()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var other = factory.CreateClient();
        await Register(client, "one@example.test");
        await Register(other, "two@example.test");

        var input = new PropertyInput("Sítio", "Ibiúna", " sp ", 3.5m, "Descrição");
        var path = Root + "/properties";
        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync(path, input)));
        Assert.All(attempts, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var first = attempts[0];
        var created = (await first.Content.ReadFromJsonAsync<PropertyResponse>())!;
        Assert.Equal("SP", created.State);
        var body = await first.Content.ReadAsStringAsync();
        Assert.EndsWith("Z", JsonDocument.Parse(body).RootElement.GetProperty("createdAt").GetString());
        foreach (var response in attempts)
        {
            Assert.Equal(first.Headers.Location, response.Headers.Location);
            Assert.Equal(body, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, (await client.GetFromJsonAsync<PageResponse<PropertyResponse>>(path))!.Page.TotalItems);
        Assert.Equal(created.Id, (await client.GetFromJsonAsync<PropertyResponse>(path + "/" + created.Id))!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/" + created.Id)).StatusCode);

        var patch = await client.PatchAsJsonAsync(path + "/" + created.Id, new { city = "Cotia", description = (string?)null });
        var updated = (await patch.Content.ReadFromJsonAsync<PropertyResponse>())!;
        Assert.Equal("Cotia", updated.City);
        Assert.Equal(created.Name, updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/" + created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/" + created.Id)).StatusCode);
    }

    [Fact]
    public async Task Explicit_key_conflicts_on_changed_content_and_field_and_planting_replay()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await Register(client, "fields@example.test");
        var properties = Root + "/properties";
        var input = new PropertyInput("Fazenda", "Ibiúna", "SP", 0.0001m);
        var first = await PostWithKey(client, properties, input, "property-1");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostWithKey(client, properties, input with { Name = "Outro" }, "property-1")).StatusCode);
        var property = (await first.Content.ReadFromJsonAsync<PropertyResponse>())!;
        var fields = $"{Root}/propriedades/{property.Id}/talhoes";
        var field = await PostWithKey(client, fields, new FieldInput("Canteiro", 1), "field-1");
        Assert.Equal(HttpStatusCode.Created, field.StatusCode);
        Assert.Equal(field.Headers.Location, (await PostWithKey(client, fields, new FieldInput("Canteiro", 1), "field-1")).Headers.Location);
        var plantings = field.Headers.Location + "/plantios";
        var planting = await client.PostAsJsonAsync(plantings, new { cropName = "Alface", plantedAt = "2026-11-10T07:00:00-03:00", stage = "SEEDLING" });
        Assert.Equal(HttpStatusCode.Created, planting.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(planting.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(first.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(field.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(planting.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task Demo_passes_device_history_alert_and_recommendation_flow()
    {
        await using var factory = new ApiFactory(seed: true);
        using var client = factory.CreateClient();
        await Login(client, "demo@agroconnect.com");
        var devices = (await client.GetFromJsonAsync<PageResponse<DeviceResponse>>(Root + "/devices"))!.Items;
        var offline = Assert.Single(devices, d => d.Status == "OFFLINE");
        var online = Assert.Single(devices, d => d.Status == "ONLINE");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Root + "/devices/" + online.Id)).StatusCode);
        var sensors = (await client.GetFromJsonAsync<PageResponse<SensorResponse>>(Root + "/devices/" + offline.Id + "/sensors"))!.Items;
        var sensor = Assert.Single(sensors);
        var readings = Root + "/devices/" + offline.Id + "/readings";
        var latest = await client.GetFromJsonAsync<LatestReadingsResponse>(readings + "/latest");
        Assert.True(latest!.SimulatedData);
        var reading = Assert.Single(latest.Readings);
        Assert.Equal("SENSOR_ERROR", reading.Quality);
        Assert.Null(reading.Value);
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));
        var history = await client.GetFromJsonAsync<HistoryResponse>(readings + "?sensorId=" + sensor.Id + "&from=" + from + "&to=" + to + "&interval=1d");
        Assert.Equal(sensor.Id, history!.SensorId);
        Assert.Null(Assert.Single(history.Items).Value);
        Assert.Null(history.Summary);
        var empty = await client.GetFromJsonAsync<HistoryResponse>(readings + "?sensorId=" + sensor.Id + "&to=2025-01-01T00:00:00Z");
        Assert.Equal(sensor.Id, empty!.SensorId);
        Assert.Equal("%", empty.Unit);
        Assert.Empty(empty.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(readings + "?type=SOIL_MOISTURE&interval=2m")).StatusCode);

        var alerts = (await client.GetFromJsonAsync<PageResponse<AlertResponse>>(Root + "/alerts?status=UNREAD"))!.Items;
        var alert = Assert.Single(alerts);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PatchAsync(Root + "/alerts/" + alert.Id + "/read", null);
            Assert.Equal("READ", (await response.Content.ReadFromJsonAsync<AlertResponse>())!.Status);
        }
        var recommended = await client.PostAsJsonAsync(Root + "/recommendations", new RecommendationInput(online.Id));
        Assert.True(Assert.Single((await recommended.Content.ReadFromJsonAsync<PageResponse<RecommendationResponse>>())!.Items).SimulatedData);
    }

    internal static async Task<AuthResponse> Register(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(Root + "/auth/register", new RegisterRequest("Pessoa de Teste", email, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return UseToken(client, (await response.Content.ReadFromJsonAsync<AuthResponse>())!);
    }

    internal static async Task<AuthResponse> Login(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(Root + "/auth/login", new LoginRequest(email, ApiFactory.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return UseToken(client, (await response.Content.ReadFromJsonAsync<AuthResponse>())!);
    }

    private static AuthResponse UseToken(HttpClient client, AuthResponse auth)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    internal static async Task<HttpResponseMessage> PostWithKey<T>(HttpClient client, string path, T input, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
