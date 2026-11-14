using Nexo.Application.Queries;
using Nexo.Application.Queries.Issuers;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

/// <summary>Emissores pelo CNPJ e as formas como os fundos escrevem o nome de cada um.</summary>
/// <remarks>Em SQL direto: escolher o nome mais usado de cada emissor não cabe bem numa consulta LINQ.</remarks>
internal sealed class IssuerQueries(NexoDbContext db) : IIssuerQueries
{
    private const int MinimumDocumentDigits = 2;

    // Primeiro junta os meses de cada fundo: a soma fica dez vezes menor e cabe na memória.
    // Nome mais usado = o que mais fundos usam; no empate, o mais recente e depois a ordem alfabética.
    private const string SummariesSql = """
        WITH uses AS (
            SELECT issuer_document, issuer_name, fund_id, max(reference_date) AS last_date
            FROM issuer_spellings
            WHERE (@document::text IS NULL OR issuer_document LIKE @document || '%')
              AND (@pattern::text IS NULL OR issuer_document IN (
                   SELECT issuer_document FROM issuer_spellings WHERE issuer_name ILIKE @pattern))
            GROUP BY issuer_document, issuer_name, fund_id
        ),
        names AS (
            SELECT issuer_document, issuer_name, count(*) AS funds, max(last_date) AS last_date
            FROM uses
            GROUP BY issuer_document, issuer_name
        ),
        issuers AS (
            SELECT issuer_document,
                   (array_agg(issuer_name ORDER BY funds DESC, last_date DESC, issuer_name))[1] AS name,
                   count(*)::int AS spellings,
                   max(last_date) AS last_date
            FROM names
            GROUP BY issuer_document
        ),
        fund_counts AS (
            SELECT issuer_document, count(DISTINCT fund_id)::int AS funds
            FROM uses
            GROUP BY issuer_document
        )
        SELECT i.issuer_document, i.name, i.spellings, f.funds, i.last_date, count(*) OVER ()::int AS total
        FROM issuers i
        JOIN fund_counts f USING (issuer_document)
        WHERE i.spellings >= @minSpellings
        """;

    private static readonly Dictionary<string, string> Orders = new()
    {
        [IssuerSearch.BySpellings] = "i.spellings DESC, f.funds DESC, i.name, i.issuer_document",
        [IssuerSearch.ByFunds] = "f.funds DESC, i.spellings DESC, i.name, i.issuer_document",
        [IssuerSearch.ByName] = "i.name, i.issuer_document",
    };

    private const string SpellingsSql = """
        SELECT issuer_name, count(DISTINCT fund_id)::int AS funds, min(reference_date) AS first_date, max(reference_date) AS last_date
        FROM issuer_spellings
        WHERE issuer_document = @document
        GROUP BY issuer_name
        ORDER BY funds DESC, last_date DESC, issuer_name
        """;

    private const string TotalsSql = """
        SELECT count(DISTINCT fund_id)::int, min(reference_date), max(reference_date)
        FROM issuer_spellings
        WHERE issuer_document = @document
        """;

    private const string FundsSql = """
        SELECT s.fund_id, f.cnpj, f.name, s.issuer_name,
               count(DISTINCT s.reference_date)::int AS months,
               min(s.reference_date) AS first_date,
               max(s.reference_date) AS last_date,
               count(*) OVER ()::int AS total
        FROM issuer_spellings s
        JOIN funds f ON f.id = s.fund_id
        WHERE s.issuer_document = @document
          AND (@spelling::text IS NULL OR s.issuer_name = @spelling)
        GROUP BY s.fund_id, f.cnpj, f.name, s.issuer_name
        ORDER BY last_date DESC, f.name, s.issuer_name
        LIMIT @take OFFSET @skip
        """;

    public async Task<PagedResult<IssuerSummary>> SearchAsync(IssuerSearch search, CancellationToken cancellationToken)
    {
        var (document, pattern) = ReadSearch(search.Search);
        var order = Orders[search.Sort ?? IssuerSearch.BySpellings];
        var sql = $"{SummariesSql}\nORDER BY {order}\nLIMIT @take OFFSET @skip";

        var total = 0;
        var items = await db.ReadAsync(
            sql,
            command =>
            {
                command.Add("document", document);
                command.Add("pattern", pattern);
                command.Add("minSpellings", search.MinSpellings ?? 1);
                command.Add("take", search.PageSize);
                command.Add("skip", search.Skip);
            },
            reader =>
            {
                total = reader.GetInt32(5);
                return new IssuerSummary(
                    reader.GetString(0).Trim(), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetFieldValue<DateOnly>(4));
            },
            cancellationToken);

        return new PagedResult<IssuerSummary>(items, search.Page, search.PageSize, total);
    }

    public async Task<IssuerDetail?> GetAsync(string document, CancellationToken cancellationToken)
    {
        var spellings = await db.ReadAsync(
            SpellingsSql,
            command => command.Add("document", document),
            reader => new IssuerSpellingView(
                reader.GetString(0), reader.GetInt32(1), reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateOnly>(3)),
            cancellationToken);

        if (spellings.Count == 0)
            return null;

        var totals = await db.ReadAsync(
            TotalsSql,
            command => command.Add("document", document),
            reader => (Funds: reader.GetInt32(0), First: reader.GetFieldValue<DateOnly>(1), Last: reader.GetFieldValue<DateOnly>(2)),
            cancellationToken);

        return new IssuerDetail(document, spellings[0].Name, totals[0].Funds, totals[0].First, totals[0].Last, spellings);
    }

    public async Task<PagedResult<IssuerFund>> GetFundsAsync(string document, IssuerFundSearch search, CancellationToken cancellationToken)
    {
        var total = 0;
        var items = await db.ReadAsync(
            FundsSql,
            command =>
            {
                command.Add("document", document);
                command.Add("spelling", string.IsNullOrWhiteSpace(search.Spelling) ? null : search.Spelling);
                command.Add("take", search.PageSize);
                command.Add("skip", search.Skip);
            },
            reader =>
            {
                total = reader.GetInt32(7);
                return new IssuerFund(
                    reader.GetInt64(0), reader.GetString(1).Trim(), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                    reader.GetFieldValue<DateOnly>(5), reader.GetFieldValue<DateOnly>(6));
            },
            cancellationToken);

        return new PagedResult<IssuerFund>(items, search.Page, search.PageSize, total);
    }

    // Texto só com números e pontuação de CNPJ é busca por CNPJ; o resto é busca por nome.
    private static (string? Document, string? Pattern) ReadSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return (null, null);

        var text = search.Trim();
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        var looksLikeDocument = text.All(character => char.IsAsciiDigit(character) || character is '.' or '/' or '-' or ' ');

        return looksLikeDocument && digits.Length >= MinimumDocumentDigits ? (digits, null) : (null, LikePattern.Contains(text));
    }
}
