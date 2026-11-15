namespace AgroConnect.Application.DTOs;

public sealed record SensorResponse(Guid Id, Guid DeviceId, string Type, string Label, string Unit);
