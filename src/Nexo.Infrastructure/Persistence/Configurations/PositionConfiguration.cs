using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Portfolios;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        // A tabela é dividida em uma partição por mês (ver PartitionedPositionsSql).
        // O PostgreSQL exige que a chave primária inclua a coluna usada para dividir.
        builder.HasKey(position => new { position.Id, position.ReferenceDate });
        builder.Property(position => position.Id).UseIdentityByDefaultColumn();

        builder.Property(position => position.Block).HasConversion<short?>();
        builder.Property(position => position.Details).HasColumnType("jsonb");

        // "numeric" sem tamanho fixo guarda o número exatamente como veio, sem arredondar.
        builder.Property(position => position.Quantity).HasColumnType("numeric");
        builder.Property(position => position.MarketValue).HasColumnType("numeric");
        builder.Property(position => position.CostValue).HasColumnType("numeric");
        builder.Property(position => position.BoughtQuantity).HasColumnType("numeric");
        builder.Property(position => position.BoughtValue).HasColumnType("numeric");
        builder.Property(position => position.SoldQuantity).HasColumnType("numeric");
        builder.Property(position => position.SoldValue).HasColumnType("numeric");
        builder.Property(position => position.ShareOfNetWorth).HasColumnType("numeric");

        builder.HasIndex(position => position.PortfolioId);
        builder.HasIndex(position => new { position.AssetId, position.ReferenceDate });

        builder.HasOne(position => position.Portfolio).WithMany().HasForeignKey(position => position.PortfolioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(position => position.Asset).WithMany().HasForeignKey(position => position.AssetId);
    }
}
