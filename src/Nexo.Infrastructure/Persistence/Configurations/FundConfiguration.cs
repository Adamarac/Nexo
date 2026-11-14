using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Funds;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> builder)
    {
        builder.Property(fund => fund.Cnpj).HasCnpjConversion();
        builder.Property(fund => fund.NetWorth).HasColumnType("numeric");

        builder.HasIndex(fund => fund.Cnpj).IsUnique();
        builder.HasIndex(fund => new { fund.FundType, fund.Situation });
        builder.HasIndex(fund => fund.Name).HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasMany(fund => fund.Classes).WithOne(fundClass => fundClass.Fund).HasForeignKey(fundClass => fundClass.FundId);
        builder.Navigation(fund => fund.Classes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
