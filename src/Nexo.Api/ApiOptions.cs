namespace Nexo.Api;

public sealed class ApiOptions
{
    public const string Section = "Api";

    // Limite de requisições por IP em cada janela de tempo.
    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 10;

    public string[] AllowedOrigins { get; set; } = [];

    // Sem chave configurada, os endpoints de administração ficam bloqueados.
    public string? AdminApiKey { get; set; }
}
