namespace AgroConnect.Application.DTOs;

public sealed class PropertyPatch
{
    private string? _description;

    public string? Name { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public decimal? AreaHectares { get; init; }

    public string? Description
    {
        get => _description;
        init { _description = value; HasDescription = true; }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasDescription { get; private set; }
}
