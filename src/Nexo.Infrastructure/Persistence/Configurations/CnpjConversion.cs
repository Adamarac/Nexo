using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Common;

namespace Nexo.Infrastructure.Persistence.Configurations;

internal static class CnpjConversion
{
    public static PropertyBuilder<Cnpj> HasCnpjConversion(this PropertyBuilder<Cnpj> property) =>
        property
            .HasConversion(cnpj => cnpj.Value, value => Cnpj.FromTrusted(value))
            .HasMaxLength(14)
            .IsFixedLength();
}
