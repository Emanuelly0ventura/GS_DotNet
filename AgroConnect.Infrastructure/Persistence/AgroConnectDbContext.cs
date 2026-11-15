using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgroConnect.Infrastructure.Persistence;

public sealed class AgroConnectDbContext(DbContextOptions<AgroConnectDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Planting> Plantings => Set<Planting>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<PushToken> PushTokens => Set<PushToken>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.IsNpgsql()) b.HasDefaultSchema("agroconnect");
        b.ApplyConfigurationsFromAssembly(typeof(AgroConnectDbContext).Assembly);
        if (Database.IsSqlite())
        {
            foreach (var entity in b.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
                        date => date.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
        }
    }
}
