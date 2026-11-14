namespace Nexo.Application.Ingestion.Parsing.Registry;

internal interface IRegistryFileParser
{
    IReadOnlyCollection<string> RequiredColumns { get; }

    ParsedRegistryRow Parse(CsvRow row);
}
