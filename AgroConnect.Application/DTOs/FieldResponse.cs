namespace AgroConnect.Application.DTOs;

public sealed record FieldResponse(Guid Id, Guid PropertyId, string Name, decimal AreaHectares);
