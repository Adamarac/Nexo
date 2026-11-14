using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Registry;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Importing;

/// <summary>Importa o cadastro de fundos e classes (registro_fundo_classe.zip).</summary>
public sealed class RegistryFileImporter(
    IDatasetArchiveReader archive, IRegistryLoader loader, ILogger<RegistryFileImporter> logger) : IDatasetImporter
{
    public DatasetKind Kind => DatasetKind.FundRegistry;

    public async Task<IngestionCounts> ImportAsync(DatasetFile file, CancellationToken cancellationToken)
    {
        await using var session = await loader.BeginAsync(file.Id, cancellationToken);
        var counter = new RowCounter();

        foreach (var csv in archive.ReadFiles(file.StoragePath))
        {
            var parser = RegistryFileParsers.For(csv.Name);

            if (parser is null)
            {
                logger.LogInformation("Arquivo {FileName} do cadastro ignorado: não é usado pelo Nexo", csv.Name);
                continue;
            }

            RequiredColumns.EnsurePresent(csv, parser.RequiredColumns);
            await ImportRowsAsync(csv, parser, session, counter, cancellationToken);
        }

        var recordsChanged = await session.CompleteAsync(cancellationToken);
        return counter.ToCounts(recordsChanged);
    }

    private static async Task ImportRowsAsync(
        CsvFile csv, IRegistryFileParser parser, IRegistryLoadSession session, RowCounter counter, CancellationToken cancellationToken)
    {
        var guard = new RowGuard(csv.Name);

        foreach (var row in csv.Rows)
        {
            var (_, guardRejection) = guard.Inspect(row);
            var parsed = guardRejection is null ? parser.Parse(row) : ParsedRegistryRow.Rejected(guardRejection);

            if (parsed.Rejection is not null)
            {
                counter.CountRejected();
                await session.AddRejectionAsync(csv.Name, parsed.Rejection, cancellationToken);
                continue;
            }

            counter.CountAccepted();

            if (parsed.Fund is not null)
                await session.AddFundAsync(parsed.Fund, cancellationToken);

            if (parsed.Class is not null)
                await session.AddClassAsync(parsed.Class, cancellationToken);
        }
    }
}
