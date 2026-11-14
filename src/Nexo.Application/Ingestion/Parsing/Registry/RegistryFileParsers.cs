namespace Nexo.Application.Ingestion.Parsing.Registry;

internal static class RegistryFileParsers
{
    // Subclasses ainda não fazem parte do Nexo, então registro_subclasse.csv é ignorado.
    public static IRegistryFileParser? For(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant() switch
        {
            "REGISTRO_FUNDO" => new RegistryFundFileParser(),
            "REGISTRO_CLASSE" => new RegistryClassFileParser(),
            _ => null,
        };
}
