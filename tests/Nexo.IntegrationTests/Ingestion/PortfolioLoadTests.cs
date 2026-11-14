using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Ingestion;
using Nexo.Domain.Ingestion;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Ingestion;

[Collection(InfrastructureCollection.Name)]
public sealed class PortfolioLoadTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    // Nas amostras: 7 fundos no PL + 2 estruturados; 884 posições e 7 linhas de PL.
    private const int SamplePortfolios = 9;
    private const int SamplePositions = 884;
    private const int SampleRows = 891;
    private const string BorealCnpj = "00102322000141";

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
    public async Task RealMonth_IsLoadedWithEveryRowAccountedFor()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());

        await _driver.CheckCvmAsync();
        var outcomes = await _driver.ProcessPendingMessagesAsync();

        outcomes.Should().Equal(ProcessingOutcome.Completed);
        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        file.Status.Should().Be(DatasetFileStatus.Processed);
        file.RowsRead.Should().Be(SampleRows);
        file.RowsRejected.Should().Be(0);
        file.PortfoliosChanged.Should().Be(SamplePortfolios);

        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(SamplePortfolios);
        (await _driver.QueryAsync(db => db.Positions.CountAsync())).Should().Be(SamplePositions);
    }

    [Fact]
    public async Task EachMonth_IsStoredInItsOwnPartition()
    {
        await LoadMonthAsync();

        var partitions = await _driver.QueryAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT child.relname AS "Value"
            FROM pg_inherits
            JOIN pg_class child ON child.oid = pg_inherits.inhrelid
            JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
            WHERE parent.relname = 'positions'
            """).ToListAsync());
        var rowsInPartition = await _driver.QueryAsync(db =>
            db.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM positions_202609""").SingleAsync());

        partitions.Should().Equal("positions_202609");
        rowsInPartition.Should().Be(SamplePositions);
    }

    [Fact]
    public async Task ShareOfNetWorth_IsCalculatedFromThePortfolioNetWorth()
    {
        await LoadMonthAsync();

        var itau = await _driver.QueryAsync(db => db.Positions
            .Where(position => position.Portfolio.Fund.Cnpj == Domain.Common.Cnpj.Parse(BorealCnpj) && position.Asset!.Ticker == "ITUB3")
            .SingleAsync());

        itau.MarketValue.Should().Be(9023562.96m);
        itau.ShareOfNetWorth.Should().BeApproximately(9023562.96m / 12004990.92m, 0.0000001m);
    }

    [Fact]
    public async Task ConfidentialPortfolio_KeepsTheTotalWithoutAnAsset()
    {
        await LoadMonthAsync();

        var confidential = await _driver.QueryAsync(db => db.Positions.Where(position => position.IsConfidential).ToListAsync());

        confidential.Should().ContainSingle();
        confidential[0].AssetId.Should().BeNull();
        confidential[0].MarketValue.Should().Be(79782916m);
    }

    [Fact]
    public async Task ProcessingTheSameFileAgain_ChangesNothing()
    {
        await LoadMonthAsync();
        var file = await _driver.LatestFileAsync(FixtureArchives.Month);

        (await _driver.RequestReprocessAsync(file.Id)).Should().Be(ReprocessResult.Queued);
        await _driver.ProcessPendingMessagesAsync();

        (await _driver.LatestFileAsync(FixtureArchives.Month)).PortfoliosChanged.Should().Be(0);
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(SamplePortfolios);
        (await _driver.QueryAsync(db => db.Positions.CountAsync())).Should().Be(SamplePositions);
    }

    [Fact]
    public async Task OlderVersionWaitingForReprocess_DoesNotReplaceTheNewerOne()
    {
        await LoadMonthAsync();
        var original = await _driver.LatestFileAsync(FixtureArchives.Month);
        await _driver.RequestReprocessAsync(original.Id);

        // A CVM republica o mês enquanto a versão antiga ainda espera na fila; a nova é processada antes.
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", RepublishedZip());
        await _driver.CheckCvmAsync();
        var republished = await _driver.LatestFileAsync(FixtureArchives.Month);
        await _driver.ProcessAsync(Application.Ingestion.Messages.DatasetDownloaded.For(republished, "nova", DateTimeOffset.UtcNow));
        var late = await _driver.ProcessAsync(Application.Ingestion.Messages.DatasetDownloaded.For(original, "antiga", DateTimeOffset.UtcNow));

        late.Should().Be(ProcessingOutcome.AlreadyProcessed);
        (await _driver.QueryAsync(db => db.DatasetFiles.SingleAsync(file => file.Id == original.Id))).Status.Should().Be(DatasetFileStatus.Superseded);
        (await BorealItauValueAsync()).Should().Be(RepublishedValue);
    }

    [Fact]
    public async Task ReprocessingAVersionThatAlreadyHasANewerOne_IsRefused()
    {
        await LoadMonthAsync();
        var original = await _driver.LatestFileAsync(FixtureArchives.Month);
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", RepublishedZip());
        await _driver.CheckCvmAsync();

        (await _driver.RequestReprocessAsync(original.Id)).Should().Be(ReprocessResult.NotAllowed);
    }

    [Fact]
    public async Task RepeatedMessage_IsIgnored()
    {
        await LoadMonthAsync();
        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        var message = Application.Ingestion.Messages.DatasetDownloaded.For(file, "repetida", DateTimeOffset.UtcNow);

        (await _driver.ProcessAsync(message)).Should().Be(ProcessingOutcome.AlreadyProcessed);
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(SamplePortfolios);
    }

    [Fact]
    public async Task Republication_CreatesARevisionOnlyForTheFundThatChanged()
    {
        await LoadMonthAsync();

        var republished = FixtureArchives.MonthZipWithReplacement("cda_fi_BLC_4_202609.csv", "186168.000000;9023562.96", "186168.000000;9100000.00");
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", republished);
        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();

        var newVersion = await _driver.LatestFileAsync(FixtureArchives.Month);
        newVersion.PortfoliosChanged.Should().Be(1);

        var boreal = await _driver.QueryAsync(db => db.Portfolios
            .Where(portfolio => portfolio.Fund.Cnpj == Domain.Common.Cnpj.Parse(BorealCnpj))
            .OrderBy(portfolio => portfolio.Revision)
            .ToListAsync());

        boreal.Select(portfolio => (portfolio.Revision, portfolio.IsCurrent)).Should().Equal((1, false), (2, true));
        (await _driver.QueryAsync(db => db.DatasetFiles.CountAsync(file => file.Status == DatasetFileStatus.Superseded))).Should().Be(1);
    }

    [Fact]
    public async Task InvalidAndDuplicatedLines_AreRejectedWithReason()
    {
        var existingLine = FixtureArchives.FirstDataLine(FixtureArchives.Month, "cda_fi_BLC_8_202609.csv");
        var zip = FixtureArchives.MonthZipWithExtraLines(
            "cda_fi_BLC_8_202609.csv",
            existingLine,
            existingLine.Replace("2026-09-30", "2026-09-31", StringComparison.Ordinal));
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", zip);

        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();

        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        file.RowsRead.Should().Be(SampleRows + 2);
        file.RowsRejected.Should().Be(2);

        var reasons = await _driver.QueryAsync(db => db.RejectedRows.Select(row => row.Reason).ToListAsync());
        reasons.Should().BeEquivalentTo([RejectionReason.DuplicateRow, RejectionReason.InvalidDate]);
    }

    [Fact]
    public async Task FileWithoutARequiredColumn_FailsWithAClearMessage()
    {
        var zip = FixtureArchives.MonthZipWithReplacement("cda_fi_PL_202609.csv", ";VL_PATRIM_LIQ", ";VALOR");
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", zip);
        await _driver.CheckCvmAsync();

        var act = () => _driver.ProcessPendingMessagesAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        file.Status.Should().Be(DatasetFileStatus.Failed);
        file.LastError.Should().Contain("VL_PATRIM_LIQ");
        (await _driver.QueryAsync(db => db.Portfolios.CountAsync())).Should().Be(0);
    }

    private const decimal RepublishedValue = 9100000.00m;

    private static byte[] RepublishedZip() =>
        FixtureArchives.MonthZipWithReplacement("cda_fi_BLC_4_202609.csv", "186168.000000;9023562.96", "186168.000000;9100000.00");

    private Task<decimal?> BorealItauValueAsync() =>
        _driver.QueryAsync(db => db.Positions
            .Where(position => position.Portfolio.IsCurrent
                && position.Portfolio.Fund.Cnpj == Domain.Common.Cnpj.Parse(BorealCnpj)
                && position.Asset!.Ticker == "ITUB3")
            .Select(position => position.MarketValue)
            .SingleAsync());

    private async Task LoadMonthAsync()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();
    }
}
