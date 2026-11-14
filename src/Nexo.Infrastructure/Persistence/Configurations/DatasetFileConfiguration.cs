using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Ingestion;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class DatasetFileConfiguration : IEntityTypeConfiguration<DatasetFile>
{
    public void Configure(EntityTypeBuilder<DatasetFile> builder)
    {
        builder.Property(file => file.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(file => file.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(file => file.ContentHash).HasMaxLength(64).IsFixedLength();

        // xmin é uma coluna interna do PostgreSQL que muda a cada atualização.
        // Usar como versão faz o EF recusar a gravação se outro processo mexeu na linha antes.
        builder.Property<uint>("Version").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.HasIndex(file => new { file.SourceKey, file.ContentHash }).IsUnique();
        builder.HasIndex(file => new { file.SourceKey, file.DiscoveredAt });
        builder.HasIndex(file => file.Status);
    }
}
