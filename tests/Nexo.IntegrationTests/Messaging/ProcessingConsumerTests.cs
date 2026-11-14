using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Ingestion;
using Nexo.Infrastructure.Messaging;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Messaging;

/// <summary>Worker rodando de verdade: outbox, RabbitMQ, consumidor, novas tentativas e DLQ.</summary>
[Collection(InfrastructureCollection.Name)]
public sealed class ProcessingConsumerTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly FakeCvmServer _cvm = new();
    private IHost _worker = null!;
    private IngestionDriver _driver = null!;

    public async Task InitializeAsync()
    {
        await RabbitQueues.PurgeAllAsync(infrastructure.RabbitMqUri);
        _worker = TestWorker.Build(infrastructure, await infrastructure.CreateDatabaseAsync(), _cvm, runBackgroundServices: true);
        _driver = new IngestionDriver(_worker.Services);
        await _worker.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _worker.StopAsync();
        _worker.Dispose();
    }

    [Fact]
    public async Task DownloadedFile_IsProcessedThroughRabbitMq()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());

        await _driver.CheckCvmAsync();

        (await Eventually.TrueAsync(() => FileHasStatusAsync(DatasetFileStatus.Processed), Timeout)).Should().BeTrue();
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(9);
    }

    [Fact]
    public async Task SameMessageDeliveredTwice_IsProcessedOnce()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        await _driver.CheckCvmAsync();
        (await Eventually.TrueAsync(() => FileHasStatusAsync(DatasetFileStatus.Processed), Timeout)).Should().BeTrue();

        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        await PublishDirectlyAsync(DatasetDownloaded.For(file, "duplicada", DateTimeOffset.UtcNow));

        (await Eventually.TrueAsync(async () => await RabbitQueues.CountAsync(infrastructure.RabbitMqUri, RabbitQueues.Process) == 0, Timeout))
            .Should().BeTrue();
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(9);
        (await _driver.LatestFileAsync(FixtureArchives.Month)).Attempts.Should().Be(1);
    }

    [Fact]
    public async Task FileThatAlwaysFails_GoesToTheDeadLetterQueueAfterTheRetries()
    {
        var file = await RegisterFileWithMissingZipAsync();

        (await Eventually.TrueAsync(async () => await RabbitQueues.CountAsync(infrastructure.RabbitMqUri, RabbitQueues.DeadLetter) == 1, Timeout))
            .Should().BeTrue();

        var failed = await _driver.QueryAsync(db => db.DatasetFiles.AsNoTracking().SingleAsync(other => other.Id == file.Id));
        failed.Status.Should().Be(DatasetFileStatus.Failed);
        failed.Attempts.Should().Be(4);
        failed.LastError.Should().NotBeNullOrEmpty();
    }

    private async Task<bool> FileHasStatusAsync(DatasetFileStatus status) =>
        await _driver.QueryAsync(db => db.DatasetFiles.AnyAsync(file => file.Status == status));

    private async Task PublishDirectlyAsync(DatasetDownloaded message)
    {
        var publisher = _worker.Services.GetRequiredService<RabbitMqPublisher>();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await publisher.PublishAsync(
            new OutgoingMessage(RabbitMqTopology.Exchange, DatasetDownloaded.RoutingKey, body)
            {
                MessageId = message.MessageId.ToString(),
                Type = "nexo.dataset-downloaded.v1",
                CorrelationId = message.CorrelationId,
            },
            CancellationToken.None);
    }

    // Um arquivo registrado cujo zip sumiu do disco falha em toda tentativa.
    private async Task<DatasetFile> RegisterFileWithMissingZipAsync()
    {
        await using var scope = _worker.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIngestionStore>();
        var file = DatasetFile.Downloaded(
            new DatasetSource("cda_fi_202609", DatasetKind.CdaMonthly, "https://cvm.teste/cda_fi_202609.zip"),
            new RemoteVersion("\"x\"", null, 10),
            new string('c', 64),
            "cda_fi_202609/nao-existe.zip",
            DateTimeOffset.UtcNow);

        await store.AddDownloadedAsync(file, saved => DatasetDownloaded.For(saved, "falha", DateTimeOffset.UtcNow), CancellationToken.None);
        return file;
    }
}
