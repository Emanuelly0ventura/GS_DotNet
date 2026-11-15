namespace AgroConnect.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset? LastSeenAt { get; set; }
    public int? Rssi { get; set; }
    public string FirmwareVersion { get; set; } = "1.0.0";
    public List<Sensor> Sensors { get; set; } = [];
}
