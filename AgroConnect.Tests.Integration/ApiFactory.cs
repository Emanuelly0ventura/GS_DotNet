using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgroConnect.Tests.Integration;

internal sealed class ApiFactory(bool seed = false, int authLimit = 20, int apiLimit = 120, string? aiHealthUrl = null, ExpoStub? expo = null)
    : WebApplicationFactory<Program>
{
    public static readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"agroconnect-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "",
            ["ConnectionStrings:Sqlite"] = $"Data Source={_database};Pooling=False;Foreign Keys=True",
            ["Database:SeedDemoData"] = seed.ToString(),
            ["Demo:Password"] = Password,
            ["RateLimiting:AuthPermitLimit"] = authLimit.ToString(),
            ["RateLimiting:PermitLimit"] = apiLimit.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
            ["Services:AiHealthUrl"] = aiHealthUrl,
            ["Push:Enabled"] = (expo is not null).ToString()
        }));
        if (expo is not null)
            builder.ConfigureServices(services => services.AddHttpClient("expo-push").ConfigurePrimaryHttpMessageHandler(() => expo));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_database);
    }
}
