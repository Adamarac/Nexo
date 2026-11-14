using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Funds;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class FundClassConfiguration : IEntityTypeConfiguration<FundClass>
{
    public void Configure(EntityTypeBuilder<FundClass> builder)
    {
        builder.Property(fundClass => fundClass.Cnpj).HasCnpjConversion();
        builder.HasIndex(fundClass => fundClass.Cnpj).IsUnique();
    }
}
