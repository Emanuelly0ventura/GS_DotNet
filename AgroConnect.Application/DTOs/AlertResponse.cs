namespace AgroConnect.Application.DTOs;

public sealed record AlertResponse(Guid Id, Guid DeviceId, string Severity, string Status, string Title, string Message, string Reason, ReadingSnapshot[] BasedOn, DateTimeOffset CreatedAt);
