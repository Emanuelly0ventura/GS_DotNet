namespace AgroConnect.Domain.Entities;

public sealed class Field
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public string Name { get; set; } = "";
    public decimal AreaHectares { get; set; }
    public List<Planting> Plantings { get; set; } = [];
    public List<Device> Devices { get; set; } = [];
}
