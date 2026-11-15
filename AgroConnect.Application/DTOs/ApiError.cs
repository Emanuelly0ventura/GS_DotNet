namespace AgroConnect.Application.DTOs;

public sealed record ApiError(string Code, string Message, object? Details, DateTimeOffset Timestamp);
