using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Assets;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.Property(asset => asset.Block).HasConversion<short>();

        builder.HasIndex(asset => asset.IdentityKey).IsUnique();
        builder.HasIndex(asset => asset.Isin);
        builder.HasIndex(asset => asset.Ticker);
        builder.HasIndex(asset => asset.Description).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
