using Microsoft.AspNetCore.Http.HttpResults;
using Nexo.Api.Filters;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Funds;
using Nexo.Application.Queries.Portfolios;

namespace Nexo.Api.Endpoints;

public static class FundEndpoints
{
    public static IEndpointRouteBuilder MapFundEndpoints(this IEndpointRouteBuilder app)
    {
        var funds = app.MapGroup("/api/v1/funds").WithTags("Fundos");

        funds.MapGet("/", SearchAsync).WithValidation<FundSearch>().WithSummary("Busca fundos por nome ou CNPJ.");
        funds.MapGet("/{fundId:long}", GetAsync).WithSummary("Cadastro do fundo, classes e datas com carteira.");
        funds.MapGet("/{fundId:long}/portfolio", GetPortfolioAsync).WithSummary("Carteira mais recente ou de uma data.");
        funds.MapGet("/{fundId:long}/portfolio/history", GetHistoryAsync).WithSummary("Evolução do PL e das posições por data.");

        return app;
    }

    private static async Task<Ok<PagedResult<FundSummary>>> SearchAsync(
        [AsParameters] FundSearch search, IFundQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.SearchAsync(search, cancellationToken));

    private static async Task<Results<Ok<FundDetails>, NotFound>> GetAsync(
        long fundId, IFundQueries queries, CancellationToken cancellationToken) =>
        await queries.GetAsync(fundId, cancellationToken) is { } fund ? TypedResults.Ok(fund) : TypedResults.NotFound();

    // Carteira é a consulta mais usada pela tela e só muda quando chega arquivo novo, então vai para o cache.
    private static async Task<Results<Ok<PortfolioView>, NotFound>> GetPortfolioAsync(
        long fundId, [AsParameters] PortfolioRequest request, IPortfolioQueries queries, IQueryCache cache, CancellationToken cancellationToken)
    {
        var cacheKey = new { fundId, request.ReferenceDate, request.ClassId };
        var portfolio = await cache.GetOrCreateAsync("fund-portfolio", cacheKey, token => queries.GetForFundAsync(fundId, request, token), cancellationToken);

        return portfolio is null ? TypedResults.NotFound() : TypedResults.Ok(portfolio);
    }

    private static async Task<Ok<IReadOnlyList<PortfolioHistoryPoint>>> GetHistoryAsync(
        long fundId, long? classId, IPortfolioQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.GetHistoryAsync(fundId, classId, cancellationToken));
}
