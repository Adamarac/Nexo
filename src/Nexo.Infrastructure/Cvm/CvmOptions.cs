namespace Nexo.Infrastructure.Cvm;

public sealed class CvmOptions
{
    public const string Section = "Cvm";

    public Uri BaseUrl { get; set; } = new("https://dados.cvm.gov.br/dados/");

    public string RegistryFolder { get; set; } = "FI/CAD/DADOS/";

    public string PortfoliosFolder { get; set; } = "FI/DOC/CDA/DADOS/";

    // Permite carregar só a partir de um mês (ex.: "2026-01") para ambientes com pouco disco.
    public string? EarliestMonth { get; set; }
}
