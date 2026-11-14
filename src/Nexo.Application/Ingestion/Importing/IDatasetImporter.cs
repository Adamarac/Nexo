using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Importing;

/// <summary>Sabe importar um tipo de arquivo da CVM. Um tipo novo de arquivo vira uma classe nova.</summary>
public interface IDatasetImporter
{
    DatasetKind Kind { get; }

    Task<IngestionCounts> ImportAsync(DatasetFile file, CancellationToken cancellationToken);
}
