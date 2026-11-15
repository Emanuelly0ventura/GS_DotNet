namespace AgroConnect.Application.DTOs;

public sealed record UserResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);
