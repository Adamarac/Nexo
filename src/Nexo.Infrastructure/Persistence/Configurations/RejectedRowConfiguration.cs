using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Ingestion;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class RejectedRowConfiguration : IEntityTypeConfiguration<RejectedRow>
{
    public void Configure(EntityTypeBuilder<RejectedRow> builder)
    {
        builder.Property(row => row.Reason).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(row => new { row.DatasetFileId, row.Reason });
        builder.HasOne<DatasetFile>().WithMany().HasForeignKey(row => row.DatasetFileId).OnDelete(DeleteBehavior.Cascade);
    }
}
