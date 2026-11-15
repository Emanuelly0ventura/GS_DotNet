namespace AgroConnect.Domain.Entities;

public sealed class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
    public List<Reading> Readings { get; set; } = [];
}
