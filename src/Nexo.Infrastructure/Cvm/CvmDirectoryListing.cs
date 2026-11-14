using System.Text.RegularExpressions;

namespace Nexo.Infrastructure.Cvm;

/// <summary>Lê a página de listagem de pastas da CVM e devolve os nomes de arquivo dos links.</summary>
internal static partial class CvmDirectoryListing
{
    public static IReadOnlyList<string> FileNames(string html) =>
        Link().Matches(html)
            .Select(match => match.Groups["file"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    // Pega só links para arquivos da própria pasta, como href="cda_fi_202609.zip".
    [GeneratedRegex(@"href=""(?<file>[A-Za-z0-9_\-]+\.(?:zip|csv))""")]
    private static partial Regex Link();
}
