using System.Text.Json;
using Nexo.Application.Ingestion.Parsing;
using Nexo.Infrastructure.Cvm;

namespace Nexo.UnitTests.Support;

/// <summary>Acesso às amostras reais da CVM guardadas em tests/fixtures/cvm.</summary>
internal static class Fixtures
{
    public const string MonthFolder = "cda_fi_202609";
    public const string RegistryFolder = "registro_fundo_classe";

    public static string Root => Path.Combine(AppContext.BaseDirectory, "fixtures", "cvm");

    public static IEnumerable<string> CsvNames(string folder) =>
        Directory.EnumerateFiles(Path.Combine(Root, folder), "*.csv").Select(Path.GetFileName).OfType<string>().Order();

    // Lê todas as linhas para a memória porque as amostras são pequenas e o arquivo é fechado logo em seguida.
    public static CsvFile Open(string folder, string fileName)
    {
        using var reader = new StreamReader(Path.Combine(Root, folder, fileName), CvmCsvReader.Encoding);
        var csv = CvmCsvReader.Open(reader, fileName);
        return csv with { Rows = csv.Rows.ToList() };
    }

    public static int ExpectedRows(string fileName)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "manifest.json")));
        return manifest.RootElement.GetProperty(fileName).GetInt32();
    }
}
