using System.IO.Compression;
using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Ports;

namespace Nexo.Infrastructure.Cvm;

/// <summary>Abre o zip guardado e entrega os CSVs um de cada vez, lendo direto do zip sem extrair.</summary>
internal sealed class ZipDatasetArchiveReader(IDatasetFileStore fileStore) : IDatasetArchiveReader
{
    // O maior CSV real tem cerca de 60 MB. O limite protege contra zips que explodem ao descompactar.
    private const long MaxEntrySize = 2L * 1024 * 1024 * 1024;

    public IEnumerable<CsvFile> ReadFiles(string storagePath)
    {
        using var stream = fileStore.OpenRead(storagePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var csvEntries = zip.Entries
            .Where(entry => entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal);

        foreach (var entry in csvEntries)
        {
            if (entry.Length > MaxEntrySize)
                throw new InvalidDataException($"O arquivo {entry.Name} passa do tamanho máximo permitido.");

            using var reader = new StreamReader(entry.Open(), CvmCsvReader.Encoding, detectEncodingFromByteOrderMarks: false);
            yield return CvmCsvReader.Open(reader, entry.Name);
        }
    }
}
