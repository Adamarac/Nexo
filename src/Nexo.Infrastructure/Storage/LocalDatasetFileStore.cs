using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Nexo.Application.Ingestion.Ports;

namespace Nexo.Infrastructure.Storage;

/// <summary>Guarda cada versão baixada em {pasta}/{arquivo}/{hash}.zip, sem nunca alterar o conteúdo.</summary>
internal sealed partial class LocalDatasetFileStore(IOptions<StorageOptions> options) : IDatasetFileStore
{
    private const string IncomingFolder = ".incoming";

    private string Root => Path.GetFullPath(options.Value.Root);

    public async Task<StoredFile> SaveAsync(string sourceKey, Stream content, CancellationToken cancellationToken)
    {
        if (!SafeName().IsMatch(sourceKey))
            throw new ArgumentException($"Nome de arquivo inesperado: '{sourceKey}'.", nameof(sourceKey));

        Directory.CreateDirectory(Path.Combine(Root, IncomingFolder));
        var temporaryPath = Path.Combine(Root, IncomingFolder, $"{Guid.NewGuid():N}.tmp");

        // O hash é calculado enquanto o arquivo é baixado, sem precisar ler tudo de novo depois.
        var (hash, size) = await CopyHashingAsync(content, temporaryPath, cancellationToken);
        var relativePath = Path.Combine(sourceKey, $"{hash}.zip");
        var finalPath = Path.Combine(Root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        if (File.Exists(finalPath))
            File.Delete(temporaryPath);
        else
            File.Move(temporaryPath, finalPath);

        return new StoredFile(relativePath.Replace('\\', '/'), hash, size);
    }

    public Stream OpenRead(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(Root, storagePath));

        // Impede que um caminho como "../../segredo" leia fora da pasta de arquivos.
        if (!fullPath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Caminho fora da pasta de arquivos: '{storagePath}'.");

        return File.OpenRead(fullPath);
    }

    private static async Task<(string Hash, long Size)> CopyHashingAsync(Stream content, string path, CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long size = 0;

        await using (var output = File.Create(path))
        {
            int read;

            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                size += read;
            }
        }

        return (Convert.ToHexStringLower(hasher.GetHashAndReset()), size);
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex SafeName();
}
