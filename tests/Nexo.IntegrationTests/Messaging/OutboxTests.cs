using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Ingestion;
using Nexo.Infrastructure.Messaging.Outbox;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Messaging;

[Collection(InfrastructureCollection.Name)]
public sealed class OutboxTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    private readonly FakeCvmServer _cvm = new();
    private IHost _worker = null!;
    private IngestionDriver _driver = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await infrastructure.CreateDatabaseAsync();
        _worker = TestWorker.Build(infrastructure, _connectionString, _cvm);
        _driver = new IngestionDriver(_worker.Services);
        await RabbitQueues.PurgeAllAsync(infrastructure.RabbitMqUri);
    }

    public Task DisposeAsync()
    {
        _worker.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FailureWhileSaving_LeavesNeitherTheFileNorTheMessage()
    {
        await using var scope = _worker.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIngestionStore>();
        var file = NewFile();

        var act = () => store.AddDownloadedAsync(file, _ => throw new InvalidOperationException("falha simulada"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _driver.QueryAsync(db => db.DatasetFiles.CountAsync())).Should().Be(0);
        (await _driver.QueryAsync(db => db.OutboxMessages.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Relay_PublishesPendingMessagesToTheWorkQueue()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        await _driver.CheckCvmAsync();

        var sent = await RunRelayAsync(_worker);

        sent.Should().Be(1);
        var delivered = await RabbitQueues.GetAsync(infrastructure.RabbitMqUri, RabbitQueues.Process);
        var message = JsonSerializer.Deserialize<DatasetDownloaded>(Encoding.UTF8.GetString(delivered!.Body.Span))!;
        message.SourceKey.Should().Be(FixtureArchives.Month);
        delivered.BasicProperties.MessageId.Should().Be(message.MessageId.ToString());
        (await _driver.QueryAsync(db => db.OutboxMessages.CountAsync(outbox => outbox.SentAt == null))).Should().Be(0);
    }

    [Fact]
    public async Task BrokerDown_KeepsTheMessageForTheNextRound()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        await _driver.CheckCvmAsync();

        using var workerWithoutBroker = TestWorker.Build(infrastructure, _connectionString, _cvm, configureServices: services =>
            services.Configure<Infrastructure.Messaging.RabbitMqOptions>(options => options.Uri = new Uri("amqp://guest:guest@localhost:1")));

        var sent = await RunRelayAsync(workerWithoutBroker);

        sent.Should().Be(0);
        var pending = await _driver.QueryAsync(db => db.OutboxMessages.SingleAsync());
        pending.SentAt.Should().BeNull();
        pending.Attempts.Should().Be(1);
        pending.LastError.Should().NotBeNullOrEmpty();
    }

    private static async Task<int> RunRelayAsync(IHost worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxRelay>().PublishPendingAsync(CancellationToken.None);
    }

    private static DatasetFile NewFile() => DatasetFile.Downloaded(
        new DatasetSource("cda_fi_202609", DatasetKind.CdaMonthly, "https://cvm.teste/cda_fi_202609.zip"),
        new RemoteVersion("\"x\"", null, 10),
        new string('b', 64),
        "cda_fi_202609/b.zip",
        DateTimeOffset.UtcNow);
}
