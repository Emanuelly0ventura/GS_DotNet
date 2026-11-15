using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroConnect.Infrastructure.Persistence.Configurations;

internal sealed class PlantingConfiguration : IEntityTypeConfiguration<Planting>
{
    public void Configure(EntityTypeBuilder<Planting> entity)
    {
        entity.HasOne(x => x.Field).WithMany(x => x.Plantings).HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
    }
}
