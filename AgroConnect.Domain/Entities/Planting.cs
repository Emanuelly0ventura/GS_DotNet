namespace AgroConnect.Domain.Entities;

public sealed class Planting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public string CropName { get; set; } = "";
    public DateTimeOffset PlantedAt { get; set; }
    public string Stage { get; set; } = "SEEDLING";
}
