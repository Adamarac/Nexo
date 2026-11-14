using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Cda.Assets;

/// <summary>Quem emitiu o ativo: documento (quando pode ser guardado) e nome.</summary>
internal sealed record Issuer(string? Document, string? Name)
{
    private const string Person = "PF";

    // Emissor pessoa física é uma pessoa comum, então não guardamos o CPF dela.
    public static Issuer Read(RowReader row, string documentColumn, string nameColumn, string? personTypeColumn = null)
    {
        var isPerson = personTypeColumn is not null && row.Text(personTypeColumn) == Person;
        var document = isPerson ? null : TextNormalizer.NormalizeDocument(row.Text(documentColumn));
        return new Issuer(document, row.Text(nameColumn));
    }

    public string? DocumentOrName => Document ?? Name;
}
