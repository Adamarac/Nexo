using Microsoft.AspNetCore.Http.HttpResults;
using Nexo.Api.Filters;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Portfolios;

namespace Nexo.Api.Endpoints;

public static class PortfolioEndpoints
{
    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var portfolios = app.MapGroup("/api/v1/portfolios").WithTags("Carteiras");

        portfolios.MapGet("/{portfolioId:long}", GetAsync).WithSummary("Uma revisão específica de carteira.");
        portfolios.MapGet("/{portfolioId:long}/positions", GetPositionsAsync)
            .WithValidation<PositionSearch>()
            .WithSummary("Posições da carteira, com filtros e paginação.");

        return app;
    }

    private static async Task<Results<Ok<PortfolioView>, NotFound>> GetAsync(
        long portfolioId, IPortfolioQueries queries, CancellationToken cancellationToken) =>
        await queries.GetAsync(portfolioId, cancellationToken) is { } portfolio ? TypedResults.Ok(portfolio) : TypedResults.NotFound();

    private static async Task<Results<Ok<PagedResult<PositionView>>, NotFound>> GetPositionsAsync(
        long portfolioId, [AsParameters] PositionSearch search, IPortfolioQueries queries, CancellationToken cancellationToken) =>
        await queries.GetPositionsAsync(portfolioId, search, cancellationToken) is { } positions
            ? TypedResults.Ok(positions)
            : TypedResults.NotFound();
}
