namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Resultado de ler uma linha de carteira: uma posição, um PL, os dois, ou uma rejeição.</summary>
public sealed record ParsedCdaRow
{
    public PositionRecord? Position { get; private init; }

    public NetWorthRecord? NetWorth { get; private init; }

    public RowRejection? Rejection { get; private init; }

    public static ParsedCdaRow Of(PositionRecord position, NetWorthRecord? netWorth = null) =>
        new() { Position = position, NetWorth = netWorth };

    public static ParsedCdaRow Of(NetWorthRecord netWorth) => new() { NetWorth = netWorth };

    public static ParsedCdaRow Rejected(RowRejection rejection) => new() { Rejection = rejection };
}
