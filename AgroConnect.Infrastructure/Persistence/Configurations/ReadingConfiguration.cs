using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroConnect.Infrastructure.Persistence.Configurations;

internal sealed class ReadingConfiguration : IEntityTypeConfiguration<Reading>
{
    public void Configure(EntityTypeBuilder<Reading> entity)
    {
        entity.HasIndex(x => new { x.SensorId, x.RecordedAt });
        entity.HasOne(x => x.Sensor).WithMany(x => x.Readings).HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(r => r.Value).HasPrecision(12, 3);
    }
}
