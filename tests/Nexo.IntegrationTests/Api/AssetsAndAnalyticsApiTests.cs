using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.Application.Queries.Dashboard;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class AssetsAndAnalyticsApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    [Fact]
    public async Task AssetSearchByTicker_FindsTheAssetAndWhoHoldsIt()
    {
        var assets = await GetAsync<PagedResult<AssetView>>("/api/v1/assets?search=ITUB3");
        var itau = assets.Items.Should().ContainSingle().Subject;

        var holders = await GetAsync<PagedResult<AssetHolder>>($"/api/v1/assets/{itau.Id}/holders");

        holders.Items.Should().ContainSingle(holder => holder.FundCnpj == "00102322000141")
            .Which.MarketValue.Should().Be(9023562.96m);
    }

    [Fact]
    public async Task Concentration_ListsTheLargestAssetsFirst()
    {
        var report = await GetAsync<ConcentrationReport>("/api/v1/analytics/concentration?top=5");

        report.ReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
        report.Items.Should().HaveCount(5);
        report.Items.Select(item => item.TotalMarketValue).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Dashboard_ShowsTotalsAndRecentFiles()
    {
        var summary = await GetAsync<DashboardSummary>("/api/v1/dashboard");

        summary.Funds.Should().Be(9);
        summary.FundsWithPortfolio.Should().Be(9);
        summary.LatestReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
        summary.RecentFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheEndpoints()
    {
        var document = await Client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        document.Should().Contain("/api/v1/funds").And.Contain("/api/v1/ingestion/files");
    }

    [Fact]
    public async Task Health_ReportsTheDatabaseIsReachable()
    {
        var response = await Client.GetAsync(new Uri("/health", UriKind.Relative));

        response.EnsureSuccessStatusCode();
    }
}
