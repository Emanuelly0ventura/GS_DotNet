namespace AgroConnect.Application.DTOs;

public sealed record ReadingResponse(Guid SensorId, string Type, decimal? Value, string Unit, string Quality, DateTimeOffset RecordedAt);
