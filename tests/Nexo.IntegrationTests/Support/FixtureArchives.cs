using System.IO.Compression;
using System.Text;

namespace Nexo.IntegrationTests.Support;

/// <summary>Monta zips iguais aos da CVM a partir das amostras reais em tests/fixtures/cvm.</summary>
public static class FixtureArchives
{
    public const string Month = "cda_fi_202609";
    public const string Registry = "registro_fundo_classe";

    public static string Root => Path.Combine(AppContext.BaseDirectory, "fixtures", "cvm");

    public static byte[] MonthZip() => ZipFolder(Month);

    public static byte[] RegistryZip() => ZipFolder(Registry);

    // Gera o zip do mês trocando o texto de uma linha, para simular uma republicação da CVM.
    public static byte[] MonthZipWithReplacement(string fileName, string oldText, string newText) =>
        ZipFolder(Month, (name, content) => name == fileName ? content.Replace(oldText, newText, StringComparison.Ordinal) : content);

    public static string FirstDataLine(string folder, string fileName) =>
        File.ReadLines(Path.Combine(Root, folder, fileName), Encoding.Latin1).Skip(1).First();

    public static byte[] MonthZipWithExtraLines(string fileName, params string[] lines) =>
        ZipFolder(Month, (name, content) => name == fileName ? content + string.Join("\r\n", lines) + "\r\n" : content);

    // Agosto/2026 montado a partir das amostras de setembro: mesmos fundos, outra data de referência.
    public static byte[] AugustZip() =>
        ZipFolder(
            Month,
            (_, content) => content.Replace("2026-09-30", "2026-08-31", StringComparison.Ordinal),
            name => name.Replace("_202609", "_202608", StringComparison.Ordinal));

    private static byte[] ZipFolder(
        string folder, Func<string, string, string>? transform = null, Func<string, string>? rename = null)
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(Root, folder), "*.csv"))
            {
                var name = Path.GetFileName(path);
                var content = File.ReadAllText(path, Encoding.Latin1);
                var entry = zip.CreateEntry(rename is null ? name : rename(name));

                using var writer = new StreamWriter(entry.Open(), Encoding.Latin1);
                writer.Write(transform is null ? content : transform(name, content));
            }
        }

        return output.ToArray();
    }
}
