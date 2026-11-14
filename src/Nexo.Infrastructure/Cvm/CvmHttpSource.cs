using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;

namespace Nexo.Infrastructure.Cvm;

/// <summary>Conversa com o portal de dados abertos da CVM por HTTP.</summary>
internal sealed partial class CvmHttpSource(HttpClient http, IOptions<CvmOptions> options) : ICvmSource
{
    private const string RegistryFile = "registro_fundo_classe.zip";

    private CvmOptions Options => options.Value;

    public async Task<IReadOnlyList<DatasetSource>> ListDatasetsAsync(CancellationToken cancellationToken)
    {
        var registryFiles = await ListFolderAsync(Options.RegistryFolder, cancellationToken);
        var portfolioFiles = await ListFolderAsync(Options.PortfoliosFolder, cancellationToken);

        var sources = new List<DatasetSource>();

        if (registryFiles.Contains(RegistryFile))
            sources.Add(Source(Options.RegistryFolder, RegistryFile, DatasetKind.FundRegistry));

        sources.AddRange(portfolioFiles
            .Where(name => MonthlyPortfolio().IsMatch(name) && IsWithinRange(name))
            .Order(StringComparer.Ordinal)
            .Select(name => Source(Options.PortfoliosFolder, name, DatasetKind.CdaMonthly)));

        return sources;
    }

    public async Task<RemoteVersion> GetRemoteVersionAsync(DatasetSource source, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, source.Url);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new RemoteVersion(
            response.Headers.ETag?.ToString(),
            response.Content.Headers.LastModified,
            response.Content.Headers.ContentLength);
    }

    // Lê em streaming: o arquivo vai direto para o disco sem passar inteiro pela memória.
    public async Task<Stream> OpenReadAsync(DatasetSource source, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private async Task<HashSet<string>> ListFolderAsync(string folder, CancellationToken cancellationToken)
    {
        var html = await http.GetStringAsync(new Uri(Options.BaseUrl, folder), cancellationToken);
        return CvmDirectoryListing.FileNames(html).ToHashSet(StringComparer.Ordinal);
    }

    private DatasetSource Source(string folder, string fileName, DatasetKind kind) =>
        new(Path.GetFileNameWithoutExtension(fileName), kind, new Uri(Options.BaseUrl, folder + fileName).ToString());

    private bool IsWithinRange(string fileName)
    {
        if (Options.EarliestMonth is null || !ReferenceMonth.TryFromFileName(fileName, out var month))
            return true;

        return string.CompareOrdinal(month.ToString(), Options.EarliestMonth) >= 0;
    }

    [GeneratedRegex(@"^cda_fi_\d{6}\.zip$")]
    private static partial Regex MonthlyPortfolio();
}
