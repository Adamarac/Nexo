using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.IntegrationTests.Support;

namespace Nexo.IntegrationTests.Api;

[Collection(InfrastructureCollection.Name)]
public sealed class AssetGroupApiTests(InfrastructureFixture infrastructure) : ApiTestBase(infrastructure)
{
    // Nas amostras há 16 LFTs, uma por vencimento.
    private const string Lft = "LETRAS FINANCEIRAS DO TESOURO";
    private const int LftMaturities = 16;

    [Fact]
    public async Task PublicBond_WithSeveralMaturities_BecomesOneGroup()
    {
        var page = await GetAsync<PagedResult<AssetGroup>>($"/api/v1/assets/groups?search={Uri.EscapeDataString(Lft)}");

        var lft = page.Items.Should().ContainSingle().Subject;
        lft.Name.Should().Be(Lft);
        lft.Assets.Should().Be(LftMaturities);
        lft.FirstMaturity.Should().Be(new DateOnly(2027, 9, 1));
        lft.Isin.Should().BeNull("o grupo tem um ISIN por vencimento");
    }

    [Fact]
    public async Task SearchByIsin_FindsOnlyThatMaturity()
    {
        var page = await GetAsync<PagedResult<AssetGroup>>("/api/v1/assets/groups?search=BRSTNCLF1RH3");

        var single = page.Items.Should().ContainSingle().Subject;
        single.Assets.Should().Be(1);
        single.Isin.Should().Be("BRSTNCLF1RH3");
        single.FirstMaturity.Should().Be(new DateOnly(2027, 9, 1));
    }

    [Fact]
    public async Task BankDeposits_AreGroupedByTypeAndIssuer_UsingTheMostUsedName()
    {
        var page = await GetAsync<PagedResult<AssetGroup>>("/api/v1/assets/groups?search=DAYCOVAL");

        // O Daycoval é escrito de dois jeitos; o grupo usa o nome que vale para o CNPJ.
        page.Items.Should().ContainSingle(group => group.AssetType == "Letra Financeira")
            .Which.Name.Should().Be("Letra Financeira · BANCO DAYCOVAL S.A.");
    }

    [Fact]
    public async Task AssetWithoutMaturity_StaysAlone()
    {
        var page = await GetAsync<PagedResult<AssetGroup>>("/api/v1/assets/groups?search=ITUB3");

        var itau = page.Items.Should().ContainSingle().Subject;
        itau.Assets.Should().Be(1);
        itau.Ticker.Should().Be("ITUB3");
    }

    [Fact]
    public async Task Maturities_ListEveryAssetOfTheGroupInDateOrder()
    {
        var page = await GetAsync<PagedResult<AssetGroup>>($"/api/v1/assets/groups?search={Uri.EscapeDataString(Lft)}");

        var group = await GetAsync<AssetGroupDetail>($"/api/v1/assets/{page.Items[0].AssetId}/maturities");

        group.Name.Should().Be(Lft);
        group.Assets.Should().HaveCount(LftMaturities);
        group.Assets.Select(asset => asset.MaturityDate).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Concentration_CanJoinTheMaturitiesOfTheSameBond()
    {
        var separate = await GetAsync<ConcentrationReport>("/api/v1/analytics/concentration?top=100");
        var grouped = await GetAsync<ConcentrationReport>("/api/v1/analytics/concentration?top=100&grouped=true");

        // O total esperado soma, vencimento por vencimento, o valor de todos os fundos que têm cada um.
        var groups = await GetAsync<PagedResult<AssetGroup>>($"/api/v1/assets/groups?search={Uri.EscapeDataString(Lft)}");
        var maturities = await GetAsync<AssetGroupDetail>($"/api/v1/assets/{groups.Items[0].AssetId}/maturities");
        var lftTotal = 0m;
        foreach (var asset in maturities.Assets)
        {
            var holders = await GetAsync<PagedResult<AssetHolder>>($"/api/v1/assets/{asset.Id}/holders?pageSize=100");
            lftTotal += holders.Items.Sum(holder => holder.MarketValue ?? 0);
        }

        var lft = grouped.Items.Should().ContainSingle(item => item.Description == Lft).Subject;

        lft.Maturities.Should().Be(LftMaturities);
        lft.TotalMarketValue.Should().Be(lftTotal);
        lft.MaturityDate.Should().BeNull();
        separate.Items.Should().AllSatisfy(item => item.Maturities.Should().Be(1));
    }
}
