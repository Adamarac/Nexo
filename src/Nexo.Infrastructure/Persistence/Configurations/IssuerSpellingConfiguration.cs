using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Assets;
using Nexo.Domain.Funds;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class IssuerSpellingConfiguration : IEntityTypeConfiguration<IssuerSpelling>
{
    public void Configure(EntityTypeBuilder<IssuerSpelling> builder)
    {
        builder.Property(spelling => spelling.IssuerDocument).HasMaxLength(14).IsFixedLength();

        // Um nome por fundo, emissor e mês; a carga apaga e regrava o mês, então não há repetição.
        builder.HasIndex(spelling => new { spelling.IssuerDocument, spelling.FundId, spelling.FundClassId, spelling.ReferenceDate, spelling.IssuerName })
            .IsUnique()
            .AreNullsDistinct(false);

        // Usado para apagar o mês de um fundo antes de regravar.
        builder.HasIndex(spelling => new { spelling.FundId, spelling.ReferenceDate });
        builder.HasIndex(spelling => spelling.IssuerName).HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasOne<Fund>().WithMany().HasForeignKey(spelling => spelling.FundId);
        builder.HasOne<FundClass>().WithMany().HasForeignKey(spelling => spelling.FundClassId);
    }
}
