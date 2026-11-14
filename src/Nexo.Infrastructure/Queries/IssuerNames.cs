using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

/// <summary>O nome mais usado pelos fundos para cada CNPJ de emissor.</summary>
internal static class IssuerNames
{
    private const int DocumentLength = 14;

    // Mesma regra da lista de emissores: mais fundos; no empate, o mais recente e depois a ordem alfabética.
    private const string MostUsedSql = """
        SELECT DISTINCT ON (issuer_document) issuer_document, issuer_name
        FROM (SELECT issuer_document, issuer_name, count(DISTINCT fund_id) AS funds, max(reference_date) AS last_date
              FROM issuer_spellings
              WHERE issuer_document = ANY(@documents::bpchar[])
              GROUP BY issuer_document, issuer_name) AS names
        ORDER BY issuer_document, funds DESC, last_date DESC, issuer_name
        """;

    public static async Task<Dictionary<string, string>> MostUsedAsync(
        this NexoDbContext db, IEnumerable<string?> documents, CancellationToken cancellationToken)
    {
        var wanted = documents.OfType<string>().Select(Pad).Distinct().ToArray();
        if (wanted.Length == 0)
            return [];

        var names = await db.ReadAsync(
            MostUsedSql,
            command => command.Add("documents", wanted),
            reader => (Document: reader.GetString(0).Trim(), Name: reader.GetString(1)),
            cancellationToken);

        return names.ToDictionary(name => name.Document, name => name.Name);
    }

    public static string Pad(string document) => document.PadLeft(DocumentLength, '0');
}
