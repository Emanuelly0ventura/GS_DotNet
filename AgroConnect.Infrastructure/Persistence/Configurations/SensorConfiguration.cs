using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroConnect.Infrastructure.Persistence.Configurations;

internal sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> entity)
    {
        entity.HasOne(x => x.Device).WithMany(x => x.Sensors).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
