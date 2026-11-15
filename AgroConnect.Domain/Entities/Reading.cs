namespace AgroConnect.Domain.Entities;

public sealed class Reading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SensorId { get; set; }
    public Sensor? Sensor { get; set; }
    public decimal? Value { get; set; }
    public string Quality { get; set; } = "VALID";
    public DateTimeOffset RecordedAt { get; set; }
    public bool SimulatedData { get; set; } = true;
}
