using System.Net;
using System.Text.Json;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Funds;
using Nexo.Application.Queries.Portfolios;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class FundAndPortfolioApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    [Fact]
    public async Task SearchByName_FindsTheFundAndItsLatestReferenceDate()
    {
        var page = await GetAsync<PagedResult<FundSummary>>("/api/v1/funds?search=boreal");

        page.TotalItems.Should().Be(1);
        page.Items[0].Cnpj.Should().Be("00102322000141");
        page.Items[0].LatestReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task SearchByFormattedCnpj_FindsTheFund()
    {
        var page = await GetAsync<PagedResult<FundSummary>>("/api/v1/funds?search=00.102.322%2F0001-41");

        page.Items.Should().ContainSingle().Which.Name.Should().StartWith("BOREAL");
    }

    [Theory]
    [InlineData("/api/v1/funds?pageSize=500", "PageSize")]
    [InlineData("/api/v1/funds?page=0", "Page")]
    [InlineData("/api/v1/funds?sort=cor", "Sort")]
    public async Task InvalidParameters_ReturnValidationProblem(string url, string field)
    {
        var response = await Client.GetAsync(new Uri(url, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }

    [Fact]
    public async Task UnknownFund_ReturnsNotFound()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/funds/999999", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FundDetails_ListClassesAndAvailableDates()
    {
        var fund = await GetAsync<FundDetails>($"/api/v1/funds/{await FundIdAsync("boreal")}");

        fund.Classes.Should().ContainSingle();
        fund.ReferenceDates.Should().Equal(new DateOnly(2026, 9, 30));
        fund.IsPlaceholder.Should().BeFalse();
    }

    [Fact]
    public async Task Portfolio_ShowsReferenceDateNetWorthAndAllocation()
    {
        var portfolio = await GetAsync<PortfolioView>($"/api/v1/funds/{await FundIdAsync("boreal")}/portfolio");

        portfolio.ReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
        portfolio.NetWorth.Should().Be(12004990.92m);
        portfolio.ProcessedAt.Should().NotBeNull();
        portfolio.Allocation.Should().Contain(slice => slice.ApplicationType == "Ações");
        portfolio.Allocation.Sum(slice => slice.Positions).Should().Be(portfolio.PositionCount);
    }

    [Fact]
    public async Task ConfidentialPortfolio_ShowsTheConfidentialValue()
    {
        var portfolio = await GetAsync<PortfolioView>($"/api/v1/funds/{await FundIdAsync("safra executive")}/portfolio");

        portfolio.ConfidentialValue.Should().Be(79782916m);
    }

    [Fact]
    public async Task Positions_ArePagedAndSortedByValue()
    {
        var portfolio = await GetAsync<PortfolioView>($"/api/v1/funds/{await FundIdAsync("daycoval")}/portfolio");

        var page = await GetAsync<PagedResult<PositionView>>($"/api/v1/portfolios/{portfolio.PortfolioId}/positions?pageSize=5");

        page.Items.Should().HaveCount(5);
        page.TotalItems.Should().Be(portfolio.PositionCount);
        page.Items.Select(position => position.MarketValue).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task History_HasOnePointPerReferenceDate()
    {
        var history = await GetAsync<List<PortfolioHistoryPoint>>($"/api/v1/funds/{await FundIdAsync("boreal")}/portfolio/history");

        history.Should().ContainSingle().Which.NetWorth.Should().Be(12004990.92m);
    }
}
