using Microsoft.AspNetCore.Http.HttpResults;
using Nexo.Api.Filters;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.Application.Queries.Dashboard;

namespace Nexo.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/analytics/concentration", GetConcentrationAsync)
            .WithTags("Indicadores")
            .WithValidation<ConcentrationSearch>()
            .WithSummary("Ativos com mais dinheiro investido, somando todos os fundos.");

        app.MapGet("/api/v1/dashboard", GetDashboardAsync)
            .WithTags("Indicadores")
            .WithSummary("Totais, última atualização e ingestões recentes.");

        return app;
    }

    // As duas consultas varrem o mês inteiro e só mudam com arquivo novo, por isso usam cache.
    private static async Task<Ok<ConcentrationReport>> GetConcentrationAsync(
        [AsParameters] ConcentrationSearch search, IAssetQueries queries, IQueryCache cache, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cache.GetOrCreateAsync(
            "concentration", new { search.ReferenceDate, search.Top }, token => queries.GetConcentrationAsync(search, token), cancellationToken));

    private static async Task<Ok<DashboardSummary>> GetDashboardAsync(
        IDashboardQueries queries, IQueryCache cache, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cache.GetOrCreateAsync("dashboard", new { }, queries.GetSummaryAsync, cancellationToken));
}
