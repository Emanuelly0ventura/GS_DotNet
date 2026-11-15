namespace AgroConnect.Domain.Rules;

public static class DomainRules
{
    public static bool IsValidProperty(string? name, string? city, string? state, decimal area) =>
        IsValidName(name) && IsValidCity(city) && IsValidState(state) && IsValidArea(area);

    public static bool IsValidName(string? name) => name?.Trim().Length is >= 2 and <= 150;
    public static bool IsValidCity(string? city) => city?.Trim().Length is >= 2 and <= 120;
    public static bool IsValidState(string? state) => state is { Length: 2 } && state.All(char.IsAsciiLetter);
    public static bool IsValidArea(decimal area) => area > 0;

    public static bool IsValidPlantingStage(string stage) =>
        new[] { "SEEDLING", "VEGETATIVE", "MATURATION", "HARVEST" }
            .Contains(stage, StringComparer.OrdinalIgnoreCase);

    public static string DeviceStatus(DateTimeOffset? lastSeenAt, DateTimeOffset now)
    {
        if (lastSeenAt is null) return "UNKNOWN";
        var age = now - lastSeenAt.Value;
        if (age <= TimeSpan.FromMinutes(5)) return "ONLINE";
        if (age <= TimeSpan.FromMinutes(30)) return "UNSTABLE";
        return "OFFLINE";
    }
    public static DateTimeOffset BucketStart(DateTimeOffset time, int minutes)
    {
        var width = TimeSpan.FromMinutes(minutes).Ticks;
        var start = time.UtcTicks - time.UtcTicks % width;
        return new DateTimeOffset(start, TimeSpan.Zero);
    }
}

