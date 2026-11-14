namespace Nexo.Application.Ingestion.Parsing.Registry;

public sealed record ParsedRegistryRow
{
    public RegistryFundRecord? Fund { get; private init; }

    public RegistryClassRecord? Class { get; private init; }

    public RowRejection? Rejection { get; private init; }

    public static ParsedRegistryRow Of(RegistryFundRecord fund) => new() { Fund = fund };

    public static ParsedRegistryRow Of(RegistryClassRecord fundClass) => new() { Class = fundClass };

    public static ParsedRegistryRow Rejected(RowRejection rejection) => new() { Rejection = rejection };
}
