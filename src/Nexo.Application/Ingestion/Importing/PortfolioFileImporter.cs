using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Cda;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Importing;

/// <summary>Importa o zip mensal de carteiras (cda_fi_AAAAMM.zip).</summary>
public sealed class PortfolioFileImporter(
    IDatasetArchiveReader archive, IPortfolioLoader loader, ILogger<PortfolioFileImporter> logger) : IDatasetImporter
{
    public DatasetKind Kind => DatasetKind.CdaMonthly;

    public async Task<IngestionCounts> ImportAsync(DatasetFile file, CancellationToken cancellationToken)
    {
        if (!ReferenceMonth.TryFromFileName(file.SourceKey, out var month))
            throw new InvalidDataException($"Não foi possível descobrir o mês do arquivo {file.SourceKey}.");

        await using var session = await loader.BeginAsync(file.Id, cancellationToken);
        var counter = new RowCounter();

        foreach (var csv in archive.ReadFiles(file.StoragePath))
        {
            var parser = CdaFileParsers.For(csv.Name, month);

            if (parser is null)
            {
                logger.LogWarning("Arquivo {FileName} ignorado: o Nexo ainda não sabe ler esse formato", csv.Name);
                continue;
            }

            RequiredColumns.EnsurePresent(csv, parser.RequiredColumns);
            await ImportRowsAsync(csv, parser, session, counter, cancellationToken);
        }

        var portfoliosChanged = await session.CompleteAsync(cancellationToken);
        return counter.ToCounts(portfoliosChanged);
    }

    private static async Task ImportRowsAsync(
        CsvFile csv, ICdaFileParser parser, IPortfolioLoadSession session, RowCounter counter, CancellationToken cancellationToken)
    {
        var guard = new RowGuard(csv.Name);

        foreach (var row in csv.Rows)
        {
            var (origin, guardRejection) = guard.Inspect(row);
            var parsed = guardRejection is null ? parser.Parse(row, origin) : ParsedCdaRow.Rejected(guardRejection);

            if (parsed.Rejection is not null)
            {
                counter.CountRejected();
                await session.AddRejectionAsync(csv.Name, parsed.Rejection, cancellationToken);
                continue;
            }

            counter.CountAccepted();

            if (parsed.Position is not null)
                await session.AddPositionAsync(parsed.Position, cancellationToken);

            if (parsed.NetWorth is not null)
                await session.AddNetWorthAsync(parsed.NetWorth, cancellationToken);
        }
    }
}
