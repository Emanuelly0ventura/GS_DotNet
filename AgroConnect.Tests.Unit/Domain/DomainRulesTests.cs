using AgroConnect.Domain.Rules;
using Xunit;

namespace AgroConnect.Tests.Unit.Domain;

public class DomainRulesTests
{
    [Theory]
    [InlineData("Sítio", "Ibiúna", "SP", 2.5, true)]
    [InlineData("A", "Ibiúna", "SP", 2.5, false)]
    [InlineData("Sítio", "Ibiúna", "São Paulo", 2.5, false)]
    [InlineData("Sítio", "Ibiúna", "SP", 0, false)]
    [InlineData("Sítio", "Ibiúna", "SP", 0.0001, true)]
    public void Validates_property_fields(string name, string city, string state, decimal area, bool expected)
        => Assert.Equal(expected, DomainRules.IsValidProperty(name, city, state, area));

    [Theory]
    [InlineData("SEEDLING", true)]
    [InlineData("VEGETATIVE", true)]
    [InlineData("MATURATION", true)]
    [InlineData("HARVEST", true)]
    [InlineData("INVALID", false)]
    public void Validates_planting_stage(string stage, bool expected)
        => Assert.Equal(expected, DomainRules.IsValidPlantingStage(stage));

    [Fact]
    public void Calculates_device_status()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("UNKNOWN", DomainRules.DeviceStatus(null, now));
        Assert.Equal("ONLINE", DomainRules.DeviceStatus(now.AddMinutes(-2), now));
        Assert.Equal("UNSTABLE", DomainRules.DeviceStatus(now.AddMinutes(-15), now));
        Assert.Equal("OFFLINE", DomainRules.DeviceStatus(now.AddHours(-1), now));
    }

    [Theory]
    [InlineData(5, "2026-11-10T13:35:00Z")]
    [InlineData(60, "2026-11-10T13:00:00Z")]
    [InlineData(1440, "2026-11-10T00:00:00Z")]
    public void Buckets_history_in_utc(int minutes, string expected)
    {
        var timestamp = DateTimeOffset.Parse("2026-11-10T10:37:03.456-03:00");
        var start = DomainRules.BucketStart(timestamp, minutes);
        Assert.Equal(DateTimeOffset.Parse(expected), start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
    }
}
