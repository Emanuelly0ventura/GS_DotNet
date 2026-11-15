namespace AgroConnect.Application.DTOs;

public sealed record PropertyResponse(Guid Id, string Name, string City, string State, decimal AreaHectares, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
