namespace AgroConnect.Application.DTOs;

public sealed record ReadingSnapshot(string Type, decimal? Value, string Unit, DateTimeOffset RecordedAt);
