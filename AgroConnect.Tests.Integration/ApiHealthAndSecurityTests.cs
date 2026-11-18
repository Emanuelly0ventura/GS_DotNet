using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroConnect.Application.DTOs;
using Xunit;

namespace AgroConnect.Tests.Integration;
using static AgroConnect.Tests.Integration.MobileContractIntegrationTests;

public sealed class ApiHealthAndSecurityTests
{
    private const string Root = "/api/v1";

    [Fact]
    public async Task Public_health_and_swagger_document_contracts()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal("ok", (await client.GetFromJsonAsync<JsonElement>("/health")).GetProperty("status").GetString());
        var ready = await client.GetFromJsonAsync<JsonElement>("/health/ready");
        Assert.Equal("Healthy", ready.GetProperty("checks").GetProperty("database").GetString());
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = spec.GetProperty("paths");
        (string Method, string Path)[] mobileRoutes =
        [
            ("post", "/auth/login"),
            ("post", "/autenticacao/cadastro"),
            ("get", "/auth/me"),
            ("post", "/autenticacao/sair"),
            ("get", "/propriedades"),
            ("post", "/propriedades"),
            ("get", "/propriedades/{id}"),
            ("patch", "/propriedades/{id}"),
            ("delete", "/propriedades/{id}"),
            ("get", "/dispositivos"),
            ("get", "/dispositivos/{id}"),
            ("get", "/dispositivos/{id}/sensores"),
            ("get", "/dispositivos/{id}/leituras/ultimas"),
            ("get", "/dispositivos/{id}/leituras"),
            ("get", "/alertas"),
            ("patch", "/alerts/{id}/read"),
            ("post", "/me/push-tokens"),
            ("post", "/recomendacoes")
        ];
        foreach (var (verb, path) in mobileRoutes)
            Assert.True(paths.TryGetProperty(Root + path, out var operations) && operations.TryGetProperty(verb, out _), $"{verb} {path}");
        var create = paths.GetProperty("/api/v1/propriedades").GetProperty("post");
        Assert.Contains(create.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.True(create.GetProperty("responses").TryGetProperty("429", out _));
        Assert.True(create.TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.Contains("Swagger UI", await client.GetStringAsync("/swagger/index.html"));
    }

    [Fact]
    public async Task Rate_limits_auth_and_api_independently_and_reports_retry_after()
    {
        await using var factory = new ApiFactory(authLimit: 2, apiLimit: 1);
        using var client = factory.CreateClient();
        await Register(client, "limited@example.test");
        await Login(client, "limited@example.test");
        var limitedLogin = await client.PostAsJsonAsync(Root + "/auth/login", new LoginRequest("limited@example.test", ApiFactory.Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Root + "/auth/me")).StatusCode);
        var limited = await client.GetAsync(Root + "/auth/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter?.Delta);
        Assert.Equal("RATE_LIMITED", (await limited.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task External_readiness_failure_returns_503_without_affecting_liveness()
    {
        await using var factory = new ApiFactory(aiHealthUrl: "http://127.0.0.1:1/health");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var body = await ready.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("recommendations").GetString());
    }
}
