namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Sabe ler as linhas de um tipo de arquivo do zip de carteiras.</summary>
internal interface ICdaFileParser
{
    IReadOnlyCollection<string> RequiredColumns { get; }

    ParsedCdaRow Parse(CsvRow row, RowOrigin origin);
}
