using Microsoft.AspNetCore.Http.HttpResults;
using Nexo.Api.Filters;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;

namespace Nexo.Api.Endpoints;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var assets = app.MapGroup("/api/v1/assets").WithTags("Ativos");

        assets.MapGet("/", SearchAsync).WithValidation<AssetSearch>().WithSummary("Busca ativos por código, ISIN ou descrição.");
        assets.MapGet("/groups", SearchGroupsAsync)
            .WithValidation<AssetSearch>()
            .WithSummary("Busca ativos juntando os vencimentos de um mesmo título numa linha só.");
        assets.MapGet("/{assetId:long}", GetAsync).WithSummary("Detalhe do ativo.");
        assets.MapGet("/{assetId:long}/maturities", GetMaturitiesAsync).WithSummary("Todos os vencimentos do título do ativo, do mais próximo ao mais distante.");
        assets.MapGet("/{assetId:long}/holders", GetHoldersAsync)
            .WithValidation<HolderSearch>()
            .WithSummary("Fundos que têm o ativo numa data, com valor e percentual do PL.");

        return app;
    }

    private static async Task<Ok<PagedResult<AssetView>>> SearchAsync(
        [AsParameters] AssetSearch search, IAssetQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.SearchAsync(search, cancellationToken));

    // Sem busca, a consulta junta todos os ativos da base; o resultado só muda com arquivo novo.
    private static async Task<Ok<PagedResult<AssetGroup>>> SearchGroupsAsync(
        [AsParameters] AssetSearch search, IAssetQueries queries, IQueryCache cache, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cache.GetOrCreateAsync("asset-groups", search, token => queries.SearchGroupsAsync(search, token), cancellationToken));

    private static async Task<Results<Ok<AssetGroupDetail>, NotFound>> GetMaturitiesAsync(
        long assetId, IAssetQueries queries, CancellationToken cancellationToken) =>
        await queries.GetMaturitiesAsync(assetId, cancellationToken) is { } group ? TypedResults.Ok(group) : TypedResults.NotFound();

    private static async Task<Results<Ok<AssetView>, NotFound>> GetAsync(
        long assetId, IAssetQueries queries, CancellationToken cancellationToken) =>
        await queries.GetAsync(assetId, cancellationToken) is { } asset ? TypedResults.Ok(asset) : TypedResults.NotFound();

    private static async Task<Ok<PagedResult<AssetHolder>>> GetHoldersAsync(
        long assetId, [AsParameters] HolderSearch search, IAssetQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.GetHoldersAsync(assetId, search, cancellationToken));
}
