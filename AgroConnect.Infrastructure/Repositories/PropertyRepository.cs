using AgroConnect.Application.Repositories;
using AgroConnect.Domain.Entities;
using AgroConnect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Infrastructure.Repositories;

public sealed class PropertyRepository(AgroConnectDbContext db) : IPropertyRepository
{
    public async Task<(IReadOnlyList<Property> Items, int Total)> ListAsync(Guid userId, int offset, int size, CancellationToken ct)
    {
        var query = OwnedBy(userId).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(offset).Take(size).ToListAsync(ct);
        return (items, total);
    }

    public Task<Property?> FindAsync(Guid userId, Guid propertyId, bool trackChanges, CancellationToken ct)
    {
        var query = OwnedBy(userId);
        if (!trackChanges) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == propertyId, ct);
    }

    public void Add(Property property) => db.Properties.Add(property);

    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    public async Task<bool> DeleteAsync(Guid userId, Guid propertyId, CancellationToken ct) =>
        await OwnedBy(userId).Where(x => x.Id == propertyId).ExecuteDeleteAsync(ct) != 0;

    private IQueryable<Property> OwnedBy(Guid userId) => db.Properties.Where(x => x.UserId == userId);
}
