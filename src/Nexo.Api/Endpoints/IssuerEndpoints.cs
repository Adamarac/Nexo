using Microsoft.AspNetCore.Http.HttpResults;
using Nexo.Api.Filters;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Issuers;

namespace Nexo.Api.Endpoints;

public static class IssuerEndpoints
{
    public static IEndpointRouteBuilder MapIssuerEndpoints(this IEndpointRouteBuilder app)
    {
        var issuers = app.MapGroup("/api/v1/issuers").WithTags("Emissores");

        issuers.MapGet("/", SearchAsync)
            .WithValidation<IssuerSearch>()
            .WithSummary("Emissores pelo CNPJ, com o nome mais usado e quantas formas de escrever o nome existem.");
        issuers.MapGet("/{document:length(14)}", GetAsync)
            .WithSummary("Cada forma de escrever o nome do emissor e quantos fundos usam cada uma.");
        issuers.MapGet("/{document:length(14)}/funds", GetFundsAsync)
            .WithValidation<IssuerFundSearch>()
            .WithSummary("Fundos que têm papéis do emissor e como cada um escreve o nome dele.");

        return app;
    }

    // A lista soma todos os meses de todos os fundos e só muda com arquivo novo, por isso usa cache.
    private static async Task<Ok<PagedResult<IssuerSummary>>> SearchAsync(
        [AsParameters] IssuerSearch search, IIssuerQueries queries, IQueryCache cache, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cache.GetOrCreateAsync("issuers", search, token => queries.SearchAsync(search, token), cancellationToken));

    private static async Task<Results<Ok<IssuerDetail>, NotFound>> GetAsync(
        string document, IIssuerQueries queries, CancellationToken cancellationToken) =>
        IsDocument(document) && await queries.GetAsync(document, cancellationToken) is { } issuer
            ? TypedResults.Ok(issuer)
            : TypedResults.NotFound();

    private static async Task<Results<Ok<PagedResult<IssuerFund>>, NotFound>> GetFundsAsync(
        string document, [AsParameters] IssuerFundSearch search, IIssuerQueries queries, CancellationToken cancellationToken) =>
        IsDocument(document) ? TypedResults.Ok(await queries.GetFundsAsync(document, search, cancellationToken)) : TypedResults.NotFound();

    private static bool IsDocument(string document) => document.All(char.IsAsciiDigit);
}
