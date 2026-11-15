using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroConnect.Infrastructure.Persistence.Configurations;

internal sealed class FieldConfiguration : IEntityTypeConfiguration<Field>
{
    public void Configure(EntityTypeBuilder<Field> entity)
    {
        entity.HasOne(x => x.Property).WithMany(x => x.Fields).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}
