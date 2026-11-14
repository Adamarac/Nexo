namespace Nexo.Domain.Portfolios;

/// <summary>Bloco do documento CDA de onde a posição veio.</summary>
public enum CdaBlock
{
    // Fundos estruturados (FIDC, FIAGRO, FIIM) mandam tudo num arquivo só, sem divisão em blocos.
    StructuredFunds = 0,
    PublicBonds = 1,
    FundShares = 2,
    Swaps = 3,
    ListedAssets = 4,
    BankDeposits = 5,
    AgribusinessAndCredit = 6,
    Foreign = 7,
    Other = 8,
}
