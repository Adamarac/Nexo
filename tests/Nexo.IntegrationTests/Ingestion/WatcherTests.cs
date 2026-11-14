using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexo.Domain.Ingestion;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Ingestion;

[Collection(InfrastructureCollection.Name)]
public sealed class WatcherTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    private readonly FakeCvmServer _cvm = new();
    private IHost _worker = null!;
    private IngestionDriver _driver = null!;

    public async Task InitializeAsync()
    {
        _worker = TestWorker.Build(infrastructure, await infrastructure.CreateDatabaseAsync(), _cvm);
        _driver = new IngestionDriver(_worker.Services);
    }

    public Task DisposeAsync()
    {
        _worker.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NewFiles_AreDownloadedAndAnnouncedThroughTheOutbox()
    {
        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());

        var summary = await _driver.CheckCvmAsync();

        summary.Downloaded.Should().Be(2);
        var files = await _driver.QueryAsync(db => db.DatasetFiles.AsNoTracking().OrderBy(file => file.Id).ToListAsync());
        files.Select(file => (file.SourceKey, file.Kind, file.Status)).Should().Equal(
            ("registro_fundo_classe", DatasetKind.FundRegistry, DatasetFileStatus.Downloaded),
            ("cda_fi_202609", DatasetKind.CdaMonthly, DatasetFileStatus.Downloaded));
        files.Should().OnlyContain(file => file.ContentHash.Length == 64);
        (await _driver.QueryAsync(db => db.OutboxMessages.CountAsync(message => message.SentAt == null))).Should().Be(2);
    }

    [Fact]
    public async Task CvmUnavailableForOneFile_DoesNotStopTheOthers()
    {
        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        _cvm.FailRequestsFor("registro_fundo_classe.zip");

        var summary = await _driver.CheckCvmAsync();

        summary.Failed.Should().Be(1);
        summary.Downloaded.Should().Be(1);
        (await _driver.LatestFileAsync(FixtureArchives.Month)).Status.Should().Be(DatasetFileStatus.Downloaded);
    }

    [Fact]
    public async Task UnchangedFiles_AreNotDownloadedAgain()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip(), etag: "\"v1\"");
        await _driver.CheckCvmAsync();

        var summary = await _driver.CheckCvmAsync();

        summary.Unchanged.Should().Be(1);
        _cvm.Downloads.Should().Be(1);
    }

    [Fact]
    public async Task NewEtagWithSameContent_DoesNotCreateANewVersion()
    {
        // O mesmo array de bytes: montar o zip de novo mudaria a data gravada dentro dele.
        var zip = FixtureArchives.MonthZip();
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", zip, etag: "\"v1\"");
        await _driver.CheckCvmAsync();
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", zip, etag: "\"v2\"");

        var summary = await _driver.CheckCvmAsync();

        summary.Unchanged.Should().Be(1);
        (await _driver.QueryAsync(db => db.DatasetFiles.CountAsync())).Should().Be(1);
        (await _driver.LatestFileAsync(FixtureArchives.Month)).ETag.Should().Be("\"v2\"");
    }

    [Fact]
    public async Task ChangedContent_CreatesANewVersion()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip(), etag: "\"v1\"");
        await _driver.CheckCvmAsync();
        var changed = FixtureArchives.MonthZipWithReplacement("cda_fi_PL_202609.csv", "1223925.76", "1223925.77");
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", changed, etag: "\"v2\"");

        var summary = await _driver.CheckCvmAsync();

        summary.Downloaded.Should().Be(1);
        (await _driver.QueryAsync(db => db.DatasetFiles.CountAsync())).Should().Be(2);
    }
}
