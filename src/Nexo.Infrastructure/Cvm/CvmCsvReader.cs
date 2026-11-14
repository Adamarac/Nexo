using System.Text;
using Nexo.Application.Ingestion.Parsing;

namespace Nexo.Infrastructure.Cvm;

/// <summary>Lê os CSVs da CVM: Latin-1, separados por ponto e vírgula, sem aspas especiais.</summary>
/// <remarks>Aspas aparecem soltas no meio dos textos, por isso não usamos um leitor de CSV comum.</remarks>
internal static class CvmCsvReader
{
    public const char Separator = ';';

    // Nenhuma linha real passa de poucos KB. Algo muito maior é arquivo corrompido ou malicioso.
    private const int MaxLineLength = 64 * 1024;

    public static Encoding Encoding => Encoding.Latin1;

    public static CsvFile Open(TextReader reader, string fileName)
    {
        var headerLine = reader.ReadLine() ?? string.Empty;
        var header = new CsvHeader(headerLine.Split(Separator));
        return new CsvFile(fileName, header, ReadRows(reader, header));
    }

    private static IEnumerable<CsvRow> ReadRows(TextReader reader, CsvHeader header)
    {
        var lineNumber = 1;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (line.Length == 0)
                continue;

            if (line.Length > MaxLineLength)
                throw new InvalidDataException($"A linha {lineNumber} passa do limite de {MaxLineLength} caracteres.");

            yield return new CsvRow(header, lineNumber, line, line.Split(Separator));
        }
    }
}
