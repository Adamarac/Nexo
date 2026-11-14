using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Ingestion;

[Collection(InfrastructureCollection.Name)]
public sealed class RegistryLoadTests(InfrastructureFixture infrastructure) : IAsyncLifetime
{
    private const int RegistryFunds = 9;

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
    public async Task Registry_KeepsOneFundPerCnpjUsingTheLatestRegistration()
    {
        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());

        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();

        var funds = await _driver.QueryAsync(db => db.Funds.AsNoTracking().ToListAsync());
        funds.Should().HaveCount(RegistryFunds);
        funds.Should().OnlyContain(fund => !fund.IsPlaceholder);
        funds.Single(fund => fund.Cnpj.Value == "00102322000141").Situation.Should().Be("Em Funcionamento Normal");
        (await _driver.QueryAsync(db => db.FundClasses.CountAsync())).Should().Be(RegistryFunds);
    }

    [Fact]
    public async Task RegistryBeforePortfolios_LinksEveryPortfolioToItsClass()
    {
        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());

        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();

        var portfolios = await _driver.QueryAsync(db => db.Portfolios.AsNoTracking().ToListAsync());
        portfolios.Should().HaveCount(9).And.OnlyContain(portfolio => portfolio.FundClassId != null);
        (await _driver.QueryAsync(db => db.Funds.CountAsync(fund => fund.IsPlaceholder))).Should().Be(0);
    }

    [Fact]
    public async Task PortfoliosBeforeRegistry_AreAttachedWhenTheRegistryArrives()
    {
        _cvm.Publish(FakeCvmServer.PortfoliosFolder, "cda_fi_202609.zip", FixtureArchives.MonthZip());
        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();
        (await _driver.QueryAsync(db => db.Funds.CountAsync(fund => fund.IsPlaceholder))).Should().Be(9);

        _cvm.Publish(FakeCvmServer.RegistryFolder, "registro_fundo_classe.zip", FixtureArchives.RegistryZip());
        await _driver.CheckCvmAsync();
        await _driver.ProcessPendingMessagesAsync();

        (await _driver.QueryAsync(db => db.Funds.CountAsync(fund => fund.IsPlaceholder))).Should().Be(0);
        var portfolios = await _driver.QueryAsync(db => db.Portfolios.AsNoTracking().ToListAsync());
        portfolios.Should().OnlyContain(portfolio => portfolio.FundClassId != null);
    }
}
