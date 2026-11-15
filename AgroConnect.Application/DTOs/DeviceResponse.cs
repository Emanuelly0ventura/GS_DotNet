namespace AgroConnect.Application.DTOs;

public sealed record DeviceResponse(Guid Id, string Code, string Name, Guid FieldId, string Status, DateTimeOffset? LastSeenAt, int? Rssi, string FirmwareVersion);
