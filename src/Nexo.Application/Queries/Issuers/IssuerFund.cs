namespace Nexo.Application.Queries.Issuers;

/// <summary>Um fundo e o jeito como ele escreve o nome do emissor, com os meses em que isso aparece.</summary>
public sealed record IssuerFund(
    long FundId,
    string FundCnpj,
    string FundName,
    string Spelling,
    int Months,
    DateOnly FirstReferenceDate,
    DateOnly LastReferenceDate);
