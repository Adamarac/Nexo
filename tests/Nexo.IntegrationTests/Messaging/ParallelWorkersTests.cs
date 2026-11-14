using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Nexo.Domain.Ingestion;
using Nexo.IntegrationTests.Support;
using Xunit.Abstractions;

namespace Nexo.IntegrationTests.Messaging;

/// <summary>Dois workers ligados na mesma fila e no mesmo banco, como no Compose com várias réplicas.</summary>
[Collection(InfrastructureCollection.Name)]
public sealed class ParallelWorkersTests(InfrastructureFixture infrastructure, ITestOutputHelper output) : IAsyncLifetime
{
    private const int PortfoliosPerMonth = 9;
    private const int PositionsPerMonth = 884;

    private readonly FakeCvmServer _cvm = new();
    private readonly List<IHost> _workers = [];
    private IngestionDriver _driver = null!;

    public async Task InitializeAsync()
    {
        await RabbitQueues.PurgeAllAsync(infrastructure.RabbitMqUri);
        var connectionString = await infrastructure.CreateDatabaseAsync();
        var sharedStorage = TestWorker.NewStorageRoot();

        for (var i = 0; i < 2; i++)
        {
            _workers.Add(TestWorker.Build(
                infrastructure,
                connectionString,
                _cvm,
                runBackgroundServices: true,
                services => services.AddLogging(logging => logging.AddProvider(new TestOutputLoggerProvider(output))),
                sharedStorage));
        }

        _driver = new IngestionDriver(_workers[0].Services);

        foreach (var worker in _workers)
            await worker.StartAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.StopAsync();
            worker.Dispose();
        }
    }

    [Fact]
    public async Task TwoWorkers_ProcessDifferentMonthsOnceEach()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202608.zip", FixtureArchives.AugustZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());

        await _driver.CheckCvmAsync();

        var bothProcessed = await Eventually.TrueAsync(
            async () => await _driver.QueryAsync(db => db.DatasetFiles.CountAsync(file => file.Status == DatasetFileStatus.Processed)) == 2,
            TimeSpan.FromSeconds(90));
        bothProcessed.Should().BeTrue();

        var files = await _driver.QueryAsync(db => db.DatasetFiles.AsNoTracking().ToListAsync());
        files.Should().OnlyContain(file => file.Attempts == 1);
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(2 * PortfoliosPerMonth);
        (await _driver.QueryAsync(db => db.Positions.CountAsync())).Should().Be(2 * PositionsPerMonth);
    }
}
