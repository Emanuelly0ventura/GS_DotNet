namespace AgroConnect.Application.DTOs;

public sealed record PropertyInput(string Name, string City, string State, decimal AreaHectares, string? Description = null);
