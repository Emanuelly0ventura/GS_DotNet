using AgroConnect.Domain.Entities;

namespace AgroConnect.Application.Repositories;

public interface IPropertyRepository
{
    Task<(IReadOnlyList<Property> Items, int Total)> ListAsync(Guid userId, int offset, int size, CancellationToken ct);
    Task<Property?> FindAsync(Guid userId, Guid propertyId, bool trackChanges, CancellationToken ct);
    void Add(Property property);
    Task SaveChangesAsync(CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid propertyId, CancellationToken ct);
}
