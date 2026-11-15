using AgroConnect.Application.DTOs;

namespace AgroConnect.Application.Services.Interfaces;

public interface IPropertyService
{
    Task<PageResponse<PropertyResponse>> ListAsync(Guid userId, int page, int size, CancellationToken ct);
    Task<PropertyResponse?> GetAsync(Guid userId, Guid id, CancellationToken ct);
    PropertyWriteResult PrepareCreation(Guid userId, PropertyInput input);
    Task<PropertyWriteResult?> UpdateAsync(Guid userId, Guid id, PropertyPatch input, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public sealed record PropertyWriteResult(PropertyResponse? Property, IReadOnlyDictionary<string, string> Errors);
