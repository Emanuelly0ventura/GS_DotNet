namespace AgroConnect.Application.DTOs;

public sealed record PlantingResponse(Guid Id, Guid FieldId, string CropName, DateTimeOffset PlantedAt, string Stage);
