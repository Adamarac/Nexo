using Nexo.Application.Ingestion.Parsing;
using Nexo.Infrastructure.Cvm;

namespace Nexo.UnitTests.Support;

/// <summary>Monta CSVs pequenos a partir de texto, para testes com linhas escritas à mão.</summary>
internal static class CsvText
{
    public static CsvFile Parse(string fileName, params string[] lines) =>
        CvmCsvReader.Open(new StringReader(string.Join("\r\n", lines)), fileName);

    public static CsvRow SingleRow(string fileName, params string[] lines) => Parse(fileName, lines).Rows.Single();
}
