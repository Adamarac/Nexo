using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Funds;
using Nexo.Application.Queries.Portfolios;
using Nexo.Domain.Ingestion;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.EndToEnd;

/// <summary>O caminho inteiro: CVM falsa → Watcher → outbox → RabbitMQ → Processing → PostgreSQL → API.</summary>
[Collection(InfrastructureCollection.Name)]
public sealed class PipelineEndToEndTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly FakeCvmServer _cvm = new();
    private IHost _worker = null!;
    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        await RabbitQueues.PurgeAllAsync(infrastructure.RabbitMqUri);
        var connectionString = await infrastructure.CreateDatabaseAsync();
        _worker = TestWorker.Build(infrastructure, connectionString, _cvm, runBackgroundServices: true);
        _api = new ApiFactory(infrastructure, connectionString);
        await _worker.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _worker.StopAsync();
        _worker.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task PublishedFiles_BecomePortfoliosInTheApi()
    {
        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        var driver = new IngestionDriver(_worker.Services);

        await driver.CheckCvmAsync();

        var processed = await Eventually.TrueAsync(
            async () => await driver.QueryAsync(db => db.DatasetFiles.CountAsync(file => file.Status == DatasetFileStatus.Processed)) == 2,
            TimeSpan.FromSeconds(60));
        processed.Should().BeTrue();

        using var client = _api.CreateClient();
        var funds = await client.GetFromJsonAsync<PagedResult<FundSummary>>("/api/v1/funds?search=boreal", Json);
        var portfolio = await client.GetFromJsonAsync<PortfolioView>($"/api/v1/funds/{funds!.Items[0].Id}/portfolio", Json);

        portfolio!.ReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
        portfolio.FundClassId.Should().NotBeNull();
        portfolio.PositionCount.Should().BeGreaterThan(0);
    }
}
