using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Ingestion;

[Collection(InfrastructureCollection.Name)]
public sealed class IssuerSpellingLoadTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    // Nas amostras, o BMG aparece escrito de dois jeitos, um por fundo.
    private const string BmgCnpj = "61186680000174";
    private const string FirstFund = "00598452000117";
    private const string SecondFund = "35591513000107";

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
    public async Task IssuerName_IsRecordedAsEachFundWroteIt()
    {
        await LoadMonthAsync(FixtureArchives.MonthZip());

        var uses = await BmgUsesAsync();

        uses.Should().BeEquivalentTo([(FirstFund, "BANCO BMG SA"), (SecondFund, "BCO BMG SA")]);
    }

    [Fact]
    public async Task FundSharesAndUnknownIssuers_AreLeftOut()
    {
        await LoadMonthAsync(FixtureArchives.MonthZip());

        var documents = await _driver.QueryAsync(db => db.IssuerSpellings.Select(spelling => spelling.IssuerDocument).Distinct().ToListAsync());

        documents.Should().NotBeEmpty();
        documents.Should().AllSatisfy(document => document.Should().MatchRegex("^[0-9]{14}$"));
        // Cota de fundo não é emissor de título, e 14 zeros é como a CVM escreve "emissor desconhecido".
        documents.Should().NotContain(["01375954000141", "00000000000000"]);
    }

    [Fact]
    public async Task ReprocessingAnUnchangedMonth_RebuildsTheNamesWithoutDuplicates()
    {
        await LoadMonthAsync(FixtureArchives.MonthZip());
        var before = await _driver.QueryAsync(db => db.IssuerSpellings.CountAsync());
        await _driver.QueryAsync(db => db.IssuerSpellings.ExecuteDeleteAsync());

        var file = await _driver.LatestFileAsync(FixtureArchives.Month);
        await _driver.RequestReprocessAsync(file.Id);
        await _driver.ProcessPendingMessagesAsync();

        // A carteira não mudou, mas os nomes voltam: é assim que os meses antigos são preenchidos.
        (await _driver.LatestFileAsync(FixtureArchives.Month)).PortfoliosChanged.Should().Be(0);
        (await _driver.QueryAsync(db => db.IssuerSpellings.CountAsync())).Should().Be(before);
    }

    [Fact]
    public async Task Republication_ReplacesTheNamesOfThatMonth()
    {
        await LoadMonthAsync(FixtureArchives.MonthZip());

        await LoadMonthAsync(FixtureArchives.MonthZipWithReplacement("cda_fi_BLC_5_202609.csv", ";BANCO BMG SA;", ";BANCO BMG S.A.;"));

        var uses = await BmgUsesAsync();
        uses.Should().BeEquivalentTo([(FirstFund, "BANCO BMG S.A."), (SecondFund, "BCO BMG SA")]);
    }

    private async Task LoadMonthAsync(byte[] zip)
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", zip);
        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();
    }

    private async Task<List<(string Fund, string Name)>> BmgUsesAsync()
    {
        var uses = await _driver.QueryAsync(db => db.IssuerSpellings
            .Where(spelling => spelling.IssuerDocument == BmgCnpj)
            .Join(db.Funds, spelling => spelling.FundId, fund => fund.Id, (spelling, fund) => new { fund.Cnpj, spelling.IssuerName })
            .ToListAsync());
        return uses.Select(use => (use.Cnpj.Value, use.IssuerName)).ToList();
    }
}
