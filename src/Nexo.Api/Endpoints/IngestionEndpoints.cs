using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Filters;
using Nexo.Application.Ingestion;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Ingestion;

namespace Nexo.Api.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var ingestion = app.MapGroup("/api/v1/ingestion").WithTags("Ingestão");

        ingestion.MapGet("/status", GetStatusAsync).WithSummary("Quantos arquivos em cada situação e avisos pendentes.");
        ingestion.MapGet("/files", ListFilesAsync).WithValidation<FileSearch>().WithSummary("Arquivos baixados da CVM.");
        ingestion.MapGet("/files/{fileId:long}", GetFileAsync).WithSummary("Detalhe de um arquivo e do seu processamento.");
        ingestion.MapGet("/files/{fileId:long}/rejections", ListRejectionsAsync)
            .WithValidation<RejectionSearch>()
            .WithSummary("Linhas rejeitadas e o motivo de cada uma.");
        ingestion.MapPost("/files/{fileId:long}/reprocess", ReprocessAsync)
            .RequireAdminKey()
            .WithSummary("Coloca o arquivo de volta na fila. Exige o cabeçalho X-Api-Key.");

        return app;
    }

    private static async Task<Ok<IngestionStatus>> GetStatusAsync(IIngestionQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.GetStatusAsync(cancellationToken));

    private static async Task<Ok<PagedResult<IngestionFileView>>> ListFilesAsync(
        [AsParameters] FileSearch search, IIngestionQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.ListFilesAsync(search, cancellationToken));

    private static async Task<Results<Ok<IngestionFileView>, NotFound>> GetFileAsync(
        long fileId, IIngestionQueries queries, CancellationToken cancellationToken) =>
        await queries.GetFileAsync(fileId, cancellationToken) is { } file ? TypedResults.Ok(file) : TypedResults.NotFound();

    private static async Task<Ok<PagedResult<RejectionView>>> ListRejectionsAsync(
        long fileId, [AsParameters] RejectionSearch search, IIngestionQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.ListRejectionsAsync(fileId, search, cancellationToken));

    private static async Task<Results<Accepted, NotFound, Conflict<ProblemDetails>>> ReprocessAsync(
        long fileId, RequestReprocess reprocess, CancellationToken cancellationToken) =>
        await reprocess.HandleAsync(fileId, cancellationToken) switch
        {
            ReprocessResult.Queued => TypedResults.Accepted((string?)null),
            ReprocessResult.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "O arquivo está sendo processado agora ou já foi substituído por uma versão mais nova.",
            }),
        };
}
