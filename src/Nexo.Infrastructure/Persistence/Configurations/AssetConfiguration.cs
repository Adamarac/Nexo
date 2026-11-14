using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Assets;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    // Títulos do governo se juntam pelo nome; CDB, debêntures e crédito privado pelo tipo e pelo CNPJ do emissor.
    // Ativos do exterior e com código de negociação ficam sozinhos: cada código já é um papel diferente.
    // O mesmo título público vem ora como "Títulos Públicos", ora como "Operações Compromissadas",
    // então o tipo de aplicação não entra na chave dele.
    private const string MaturityGroupSql = """
        CASE
            WHEN maturity_date IS NULL OR ticker IS NOT NULL OR block NOT IN (0, 1, 5, 6) THEN NULL
            WHEN block = 1 THEN 'bond|' || coalesce(description, '')
            WHEN issuer_document IS NOT NULL THEN 'issuer|' || coalesce(application_type, '') || '|' || coalesce(asset_type, '') || '|' || issuer_document
            ELSE 'name|' || coalesce(application_type, '') || '|' || coalesce(description, '')
        END
        """;

    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.Property(asset => asset.Block).HasConversion<short>();
        builder.Property(asset => asset.MaturityGroup).HasComputedColumnSql(MaturityGroupSql, stored: true);

        builder.HasIndex(asset => asset.IdentityKey).IsUnique();
        builder.HasIndex(asset => asset.Isin);
        builder.HasIndex(asset => asset.Ticker);
        builder.HasIndex(asset => asset.MaturityGroup);
        builder.HasIndex(asset => asset.Description).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
