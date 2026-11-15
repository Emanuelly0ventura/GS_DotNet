using AgroConnect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroConnect.Infrastructure.Persistence.Configurations;

internal sealed class PushTokenConfiguration : IEntityTypeConfiguration<PushToken>
{
    public void Configure(EntityTypeBuilder<PushToken> entity)
    {
        entity.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User).WithMany(x => x.PushTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => x.Token).IsUnique();
    }
}
