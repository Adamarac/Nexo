using System.Text.RegularExpressions;

namespace Nexo.Domain.Common;

/// <summary>Mês a que um arquivo de carteiras se refere, como 2026-09.</summary>
public readonly partial record struct ReferenceMonth(int Year, int Month)
{
    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    // Os arquivos da CVM terminam com AAAAMM, como cda_fi_BLC_4_202609.csv.
    public static bool TryFromFileName(string fileName, out ReferenceMonth month)
    {
        month = default;
        var match = TrailingYearMonth().Match(Path.GetFileNameWithoutExtension(fileName));

        if (!match.Success)
            return false;

        var year = int.Parse(match.Groups["year"].ValueSpan, provider: null);
        var monthNumber = int.Parse(match.Groups["month"].ValueSpan, provider: null);

        if (monthNumber is < 1 or > 12)
            return false;

        month = new ReferenceMonth(year, monthNumber);
        return true;
    }

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public override string ToString() => $"{Year:D4}-{Month:D2}";

    [GeneratedRegex(@"_(?<year>\d{4})(?<month>\d{2})$")]
    private static partial Regex TrailingYearMonth();
}
