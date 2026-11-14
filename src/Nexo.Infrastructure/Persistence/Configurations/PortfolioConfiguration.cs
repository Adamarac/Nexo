using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Ingestion;
using Nexo.Domain.Portfolios;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.Property(portfolio => portfolio.NetWorth).HasColumnType("numeric");
        builder.Property(portfolio => portfolio.Fingerprint).HasMaxLength(64).IsFixedLength();

        builder.HasIndex(portfolio => new { portfolio.FundId, portfolio.FundClassId, portfolio.ReferenceDate, portfolio.Revision })
            .IsUnique()
            .AreNullsDistinct(false);

        // Garante no banco que cada fundo tem no máximo uma revisão atual por data.
        builder.HasIndex(portfolio => new { portfolio.FundId, portfolio.FundClassId, portfolio.ReferenceDate })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("is_current")
            .HasDatabaseName("ix_portfolios_current");

        builder.HasIndex(portfolio => new { portfolio.ReferenceDate, portfolio.IsCurrent });
        builder.HasIndex(portfolio => portfolio.DatasetFileId);

        builder.HasOne(portfolio => portfolio.Fund).WithMany().HasForeignKey(portfolio => portfolio.FundId);
        builder.HasOne(portfolio => portfolio.FundClass).WithMany().HasForeignKey(portfolio => portfolio.FundClassId);
        builder.HasOne<DatasetFile>().WithMany().HasForeignKey(portfolio => portfolio.DatasetFileId);
    }
}
