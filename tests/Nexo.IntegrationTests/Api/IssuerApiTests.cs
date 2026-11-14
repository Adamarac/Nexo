using System.Net;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Issuers;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class IssuerApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    // Nas amostras, três emissores aparecem escritos de mais de um jeito.
    private const string Bmg = "61186680000174";
    private const string BrPartners = "13220493000117";
    private const string Daycoval = "62232889000190";

    [Fact]
    public async Task Search_FindsTheIssuerByAnyOfItsNames()
    {
        var page = await GetAsync<PagedResult<IssuerSummary>>("/api/v1/issuers?search=BCO%20BMG");

        var bmg = page.Items.Should().ContainSingle().Subject;
        bmg.Document.Should().Be(Bmg);
        bmg.Spellings.Should().Be(2);
        bmg.Funds.Should().Be(2);
        bmg.LastReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task Search_AcceptsTheCnpjWithPunctuation()
    {
        var page = await GetAsync<PagedResult<IssuerSummary>>("/api/v1/issuers?search=61.186.680%2F0001-74");

        page.Items.Should().ContainSingle().Which.Document.Should().Be(Bmg);
    }

    [Fact]
    public async Task OnlyIssuersWrittenInMoreThanOneWay_CanBeListed()
    {
        var page = await GetAsync<PagedResult<IssuerSummary>>("/api/v1/issuers?minSpellings=2&pageSize=100");

        page.Items.Select(issuer => issuer.Document).Should().BeEquivalentTo([Bmg, BrPartners, Daycoval]);
        page.TotalItems.Should().Be(3);
    }

    [Fact]
    public async Task Detail_ListsEachNameWithHowManyFundsUseIt()
    {
        var daycoval = await GetAsync<IssuerDetail>($"/api/v1/issuers/{Daycoval}");

        // Empate em número de fundos: vale a ordem alfabética, para o nome não mudar entre consultas.
        daycoval.Name.Should().Be("BANCO DAYCOVAL S.A.");
        daycoval.Funds.Should().Be(2);
        daycoval.Spellings.Select(spelling => (spelling.Name, spelling.Funds))
            .Should().Equal(("BANCO DAYCOVAL S.A.", 2), ("BCO DAYCOVAL SA", 2));
    }

    [Fact]
    public async Task Funds_ShowHowEachFundWritesTheIssuer()
    {
        var all = await GetAsync<PagedResult<IssuerFund>>($"/api/v1/issuers/{Bmg}/funds");
        var filtered = await GetAsync<PagedResult<IssuerFund>>($"/api/v1/issuers/{Bmg}/funds?spelling=BCO%20BMG%20SA");

        all.Items.Select(fund => fund.Spelling).Should().BeEquivalentTo(["BANCO BMG SA", "BCO BMG SA"]);
        all.Items.Should().AllSatisfy(fund =>
        {
            fund.FundName.Should().NotBeNullOrWhiteSpace();
            fund.Months.Should().Be(1);
            fund.LastReferenceDate.Should().Be(new DateOnly(2026, 9, 30));
        });
        filtered.Items.Should().ContainSingle().Which.Spelling.Should().Be("BCO BMG SA");
    }

    [Theory]
    [InlineData("/api/v1/issuers/11111111000111")]
    [InlineData("/api/v1/issuers/123")]
    public async Task UnknownIssuer_IsNotFound(string url)
    {
        var response = await Client.GetAsync(new Uri(url, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidFilters_AreRejected()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/issuers?minSpellings=0&sort=bogus", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
