namespace Nexo.Application.Queries.Issuers;

/// <summary>Um emissor com cada forma de escrever o nome, da mais usada para a menos usada.</summary>
public sealed record IssuerDetail(
    string Document,
    string Name,
    int Funds,
    DateOnly FirstReferenceDate,
    DateOnly LastReferenceDate,
    IReadOnlyList<IssuerSpellingView> Spellings);

public sealed record IssuerSpellingView(
    string Name,
    int Funds,
    DateOnly FirstReferenceDate,
    DateOnly LastReferenceDate);
