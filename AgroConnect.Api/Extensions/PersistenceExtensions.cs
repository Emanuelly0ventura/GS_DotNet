using AgroConnect.Application.Repositories;
using AgroConnect.Application.Services.Implementations;
using AgroConnect.Application.Services.Interfaces;
using AgroConnect.Infrastructure.Persistence;
using AgroConnect.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Api.Extensions;

public static class PersistenceExtensions
{
    public static void AddAgroConnectPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AgroConnectDbContext>((services, options) =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var postgres = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(postgres))
            {
                options.UseNpgsql(postgres);
                return;
            }
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection para PostgreSQL.");
            var sqlite = configuration.GetConnectionString("Sqlite") ??
                $"Data Source={Path.Combine(environment.ContentRootPath, "agroconnect.db")};Foreign Keys=True";
            options.UseSqlite(sqlite);
        });
        builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
        builder.Services.AddScoped<IPropertyService, PropertyService>();
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroConnectDbContext>();
            if (db.Database.IsSqlite()) await db.Database.EnsureCreatedAsync();
            else if (app.Configuration.GetValue("Database:ApplyMigrations", false)) await db.Database.MigrateAsync();
            if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:SeedDemoData", false))
            {
                var demoPassword = app.Configuration["Demo:Password"];
                if (demoPassword?.Length is not (>= 8 and <= 128))
                    throw new InvalidOperationException("Configure Demo__Password de 8 a 128 caracteres para criar a demonstração.");
                await DemoDataSeeder.SeedAsync(db, demoPassword);
            }
        }
    }
}
