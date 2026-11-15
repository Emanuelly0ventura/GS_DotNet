namespace AgroConnect.Application.DTOs;

public sealed record HistoryResponse(Guid DeviceId, Guid SensorId, string Type, string Unit, string Interval, IReadOnlyList<HistoryPoint> Items, HistorySummary? Summary, bool SimulatedData);
