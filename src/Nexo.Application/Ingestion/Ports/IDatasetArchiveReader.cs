using Nexo.Application.Ingestion.Parsing;

namespace Nexo.Application.Ingestion.Ports;

/// <summary>Abre um zip guardado e entrega os CSVs de dentro dele, linha a linha.</summary>
public interface IDatasetArchiveReader
{
    // Leia todas as linhas de um CSV antes de pedir o próximo: o zip é lido em sequência, sem extrair.
    IEnumerable<CsvFile> ReadFiles(string storagePath);
}
