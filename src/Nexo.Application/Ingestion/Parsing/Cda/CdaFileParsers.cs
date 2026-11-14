using System.Text.RegularExpressions;
using Nexo.Application.Ingestion.Parsing.Cda.Assets;
using Nexo.Domain.Common;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Escolhe o leitor certo pelo nome do arquivo dentro do zip.</summary>
internal static partial class CdaFileParsers
{
    private static readonly Dictionary<int, Func<IAssetDescriber>> DescribersByBlock = new()
    {
        [1] = () => new PublicBondDescriber(),
        [2] = () => new FundShareDescriber(),
        [3] = () => new SwapDescriber(),
        [4] = () => new ListedAssetDescriber(),
        [5] = () => new BankDepositDescriber(),
        [6] = () => new PrivateCreditDescriber(),
        [7] = () => new ForeignAssetDescriber(),
        [8] = () => new OtherAssetDescriber(),
    };

    // Devolve null para arquivos que ainda não sabemos ler; eles são registrados no log e ignorados.
    public static ICdaFileParser? For(string fileName, ReferenceMonth month)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();

        if (name.StartsWith("CDA_FI_PL_", StringComparison.Ordinal))
            return new NetWorthFileParser(month);

        if (ConfidentialFile().IsMatch(name))
            return new ConfidentialFileParser(month);

        if (StructuredFile().IsMatch(name))
            return new StructuredFileParser(month);

        var blockMatch = BlockFile().Match(name);

        if (!blockMatch.Success)
            return null;

        var blockNumber = int.Parse(blockMatch.Groups["block"].ValueSpan, provider: null);

        return DescribersByBlock.TryGetValue(blockNumber, out var createDescriber)
            ? new BlockFileParser((CdaBlock)blockNumber, createDescriber(), month)
            : null;
    }

    [GeneratedRegex(@"^CDA_FI(E|IM)?_CONFID_\d{6}$")]
    private static partial Regex ConfidentialFile();

    [GeneratedRegex(@"^CDA_FI(E|IM)_\d{6}$")]
    private static partial Regex StructuredFile();

    [GeneratedRegex(@"^CDA_FI_BLC_(?<block>\d)_\d{6}$")]
    private static partial Regex BlockFile();
}
