namespace Nexo.Application.Queries.Issuers;

/// <summary>Um emissor na lista: o nome mais usado pelos fundos e quantas formas de escrever existem.</summary>
public sealed record IssuerSummary(
    string Document,
    string Name,
    int Spellings,
    int Funds,
    DateOnly LastReferenceDate);
