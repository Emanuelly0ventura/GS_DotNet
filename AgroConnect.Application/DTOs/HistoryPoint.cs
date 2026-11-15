namespace AgroConnect.Application.DTOs;

public sealed record HistoryPoint(DateTimeOffset Timestamp, decimal? Value, decimal? Min, decimal? Max, int SampleCount, string Quality);
