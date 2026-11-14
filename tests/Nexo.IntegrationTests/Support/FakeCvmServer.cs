using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Nexo.IntegrationTests.Support;

/// <summary>Imita o servidor de dados abertos da CVM, para os testes não dependerem da internet.</summary>
public sealed class FakeCvmServer : HttpMessageHandler
{
    public const string RegistryFolder = "FI/CAD/DADOS/";
    public const string PortfoliosFolder = "FI/DOC/CDA/DADOS/";

    public static readonly Uri BaseUrl = new("https://cvm.teste/dados/");

    private readonly Dictionary<string, PublishedFile> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    public int Downloads { get; private set; }

    public void Publish(string folder, string fileName, byte[] content, string? etag = null) =>
        _files[fileName] = new PublishedFile(folder, content, etag ?? $"\"{Guid.NewGuid():N}\"");

    // Simula o servidor da CVM fora do ar para um arquivo específico.
    public void FailRequestsFor(string fileName) => _failing.Add(fileName);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath[BaseUrl.AbsolutePath.Length..];

        if (path is RegistryFolder or PortfoliosFolder)
            return Task.FromResult(Listing(path));

        var fileName = Path.GetFileName(path);

        if (_failing.Contains(fileName))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        if (!_files.TryGetValue(fileName, out var file))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        if (request.Method == HttpMethod.Get)
            Downloads++;

        return Task.FromResult(FileResponse(file, includeBody: request.Method == HttpMethod.Get));
    }

    private HttpResponseMessage Listing(string folder)
    {
        var links = _files.Where(file => file.Value.Folder == folder).Select(file => $"<a href=\"{file.Key}\">{file.Key}</a>");
        var html = $"<html><body><a href=\"../\">../</a>{string.Join(string.Empty, links)}</body></html>";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };
    }

    private static HttpResponseMessage FileResponse(PublishedFile file, bool includeBody)
    {
        var content = new ByteArrayContent(includeBody ? file.Content : []);
        content.Headers.ContentLength = file.Content.Length;
        content.Headers.LastModified = DateTimeOffset.UtcNow;

        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        response.Headers.ETag = EntityTagHeaderValue.Parse(file.ETag);
        return response;
    }

    private sealed record PublishedFile(string Folder, byte[] Content, string ETag);
}
