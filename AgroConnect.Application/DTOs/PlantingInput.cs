namespace AgroConnect.Application.DTOs;

public sealed record PlantingInput(string CropName, DateTimeOffset PlantedAt, string Stage);
