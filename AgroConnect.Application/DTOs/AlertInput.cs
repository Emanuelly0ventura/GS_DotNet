namespace AgroConnect.Application.DTOs;

public sealed record AlertInput(Guid DeviceId, string Severity, string Title, string Message, string Reason);
