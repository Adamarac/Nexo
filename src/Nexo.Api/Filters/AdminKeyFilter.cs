using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Nexo.Api.Filters;

/// <summary>Só deixa passar quem manda a chave de administração no cabeçalho X-Api-Key.</summary>
internal sealed class AdminKeyFilter(IOptions<ApiOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.AdminApiKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expected) || !AreEqual(expected, provided))
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Chave de administração ausente ou inválida.");

        return await next(context);
    }

    // Compara em tempo constante para não dar pistas da chave pelo tempo de resposta.
    private static bool AreEqual(string expected, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
}
