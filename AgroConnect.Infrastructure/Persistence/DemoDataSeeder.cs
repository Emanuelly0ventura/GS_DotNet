using AgroConnect.Application.Security;
using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(AgroConnectDbContext db, string password, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == "demo@agroconnect.com", ct)) return;

        var now = DateTimeOffset.UtcNow;
        var user = new User { Name = "Agricultor Demonstração", Email = "demo@agroconnect.com", PasswordHash = PasswordHasher.Hash(password) };
        var property = new Property { User = user, Name = "Sítio Boa Esperança", City = "Ibiúna", State = "SP", AreaHectares = 3.5m, Description = "Propriedade de demonstração" };
        var field = new Field { Property = property, Name = "Talhão A - Hortaliças", AreaHectares = 1.2m };
        var field2 = new Field { Property = property, Name = "Talhão B - Teste", AreaHectares = 0.8m };
        var planting = new Planting { Field = field, CropName = "Alface", PlantedAt = now.AddDays(-18), Stage = "VEGETATIVE" };
        var device = new Device { Field = field, Code = "ESP32-001", Name = "Sensor do Canteiro de Alface", LastSeenAt = now.AddMinutes(-1), Rssi = -61, FirmwareVersion = "1.0.0" };
        var offline = new Device { Field = field2, Code = "ESP32-002", Name = "Estação de monitoramento offline", LastSeenAt = now.AddHours(-3), Rssi = null, FirmwareVersion = "1.0.0" };
        var sensors = new[]
        {
            new Sensor { Device = device, Type = "SOIL_MOISTURE", Label = "Umidade do solo", Unit = "%" },
            new Sensor { Device = device, Type = "AIR_TEMPERATURE", Label = "Temperatura do ar", Unit = "°C" },
            new Sensor { Device = device, Type = "AIR_HUMIDITY", Label = "Umidade do ar", Unit = "%" },
            new Sensor { Device = device, Type = "LIGHT_ILLUMINANCE", Label = "Luminosidade", Unit = "lux" },
            new Sensor { Device = offline, Type = "SOIL_MOISTURE", Label = "Umidade do solo", Unit = "%" }
        };
        db.AddRange(user, property, field, field2, planting, device, offline);
        db.Sensors.AddRange(sensors);
        var values = new decimal[] { 24.5m, 26.2m, 63m, 12500m };
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 24; j++)
            {
                var variation = (decimal)Math.Sin(j / 3.0) * (i == 0 ? 2m : 1m);
                db.Readings.Add(new Reading { Sensor = sensors[i], Value = values[i] + variation, Quality = "VALID", RecordedAt = now.AddHours(-23 + j), SimulatedData = true });
            }
        }
        db.Readings.Add(new Reading { Sensor = sensors[4], Value = null, Quality = "SENSOR_ERROR", RecordedAt = now.AddHours(-3), SimulatedData = true });
        db.Readings.Add(new Reading { Sensor = sensors[0], Value = null, Quality = "UNCALIBRATED", RecordedAt = now.AddMinutes(-10), SimulatedData = true });
        db.Alerts.Add(new Alert { Device = device, Severity = "WARNING", Status = "UNREAD", Title = "Umidade do solo abaixo do esperado", Message = "A leitura recente indica umidade relativamente baixa.", Reason = "Valor demonstrativo abaixo da faixa de referência configurada para este cenário.", BasedOnJson = System.Text.Json.JsonSerializer.Serialize(new[] { new { type = "SOIL_MOISTURE", value = 24.5, unit = "%", recordedAt = now.UtcDateTime.ToString("O") } }), CreatedAt = now });
        await db.SaveChangesAsync(ct);
    }
}

