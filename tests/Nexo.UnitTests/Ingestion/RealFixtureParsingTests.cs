using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Cda;
using Nexo.Application.Ingestion.Parsing.Registry;
using Nexo.Domain.Common;
using Nexo.Domain.Portfolios;
using Nexo.UnitTests.Support;

namespace Nexo.UnitTests.Ingestion;

/// <summary>Lê as amostras reais de setembro/2026 e confere que nada foi rejeitado sem motivo.</summary>
public sealed class RealFixtureParsingTests
{
    private static readonly ReferenceMonth September = new(2026, 9);

    public static TheoryData<string> MonthFiles() => [.. Fixtures.CsvNames(Fixtures.MonthFolder)];

    [Theory]
    [MemberData(nameof(MonthFiles))]
    public void EveryRealRow_IsAccepted(string fileName)
    {
        var rows = ParseMonthFile(fileName);

        rows.Should().HaveCount(Fixtures.ExpectedRows(fileName));
        rows.Where(row => row.Rejection is not null).Select(row => row.Rejection!.Detail).Should().BeEmpty();
    }

    [Fact]
    public void ListedAsset_IsIdentifiedByIsin()
    {
        var itau = ParseMonthFile("cda_fi_BLC_4_202609.csv")
            .Select(row => row.Position!)
            .Single(position => position.Fund.Cnpj.Value == "00102322000141" && position.Asset!.Ticker == "ITUB3");

        itau.Block.Should().Be(CdaBlock.ListedAssets);
        itau.Asset!.Identity.Key.Should().Be("isin:BRITUBACNOR4");
        itau.Amounts.Quantity.Should().Be(186168m);
        itau.Amounts.MarketValue.Should().Be(9023562.96m);
        itau.ApplicationType.Should().Be("Ações");
    }

    [Fact]
    public void SameDebentureForTradingAndHeldToMaturity_IsNotADuplicate()
    {
        var lorta = ParseMonthFile("cda_fi_BLC_4_202609.csv")
            .Select(row => row.Position!)
            .Where(position => position.Asset!.Ticker == "LORTA7")
            .ToList();

        lorta.Should().HaveCount(2);
        lorta.Select(position => position.Asset!.Identity).Distinct().Should().ContainSingle();
        lorta.Select(position => position.TradingIntent).Should().BeEquivalentTo("Para negociação", "Mantido até o vencimento");
    }

    [Fact]
    public void ConfidentialRow_HasValueButNoAsset()
    {
        var confidential = ParseMonthFile("cda_fi_CONFID_202609.csv").Single().Position!;

        confidential.IsConfidential.Should().BeTrue();
        confidential.Asset.Should().BeNull();
        confidential.Amounts.MarketValue.Should().Be(79782916m);
        confidential.ConfidentialUntil.Should().Be(new DateOnly(2026, 12, 29));
    }

    [Fact]
    public void NegativeValues_AreKeptWithTheirSign()
    {
        var positions = ParseMonthFile("cda_fi_BLC_8_202609.csv").Select(row => row.Position!);

        positions.Should().Contain(position => position.Amounts.MarketValue < 0);
    }

    [Fact]
    public void StructuredFunds_RegisterNetWorthOncePerFund()
    {
        var rows = ParseMonthFile("cda_fie_202609.csv");

        var netWorths = rows.Where(row => row.NetWorth is not null).Select(row => row.NetWorth!).ToList();
        netWorths.Should().HaveCount(2);
        netWorths.Should().Contain(netWorth => netWorth.Fund.Cnpj.Value == "10947525000192" && netWorth.NetWorth == 5342081835.70m);
    }

    [Fact]
    public void StructuredFunds_IdentifyFundSharesByIssuerCnpj()
    {
        var shares = ParseMonthFile("cda_fie_202609.csv")
            .Select(row => row.Position!)
            .Where(position => position.ApplicationType == "Cotas de Fundos");

        shares.Select(position => position.Asset!.Identity.Key).Should().Contain("fund:26845679000103");
    }

    [Fact]
    public void Registry_KeepsEveryRegistrationOfTheSameFund()
    {
        var csv = Fixtures.Open(Fixtures.RegistryFolder, "registro_fundo.csv");
        var parser = RegistryFileParsers.For(csv.Name)!;

        var funds = csv.Rows.Select(parser.Parse).Select(row => row.Fund!).ToList();

        funds.Should().HaveCount(Fixtures.ExpectedRows("registro_fundo.csv"));
        funds.Where(fund => fund.Cnpj.Value == "00102322000141").Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Registry_LinksClassToFundRegistration()
    {
        var csv = Fixtures.Open(Fixtures.RegistryFolder, "registro_classe.csv");
        var parser = RegistryFileParsers.For(csv.Name)!;

        var boreal = csv.Rows.Select(parser.Parse).Select(row => row.Class!).Single(fundClass => fundClass.Cnpj.Value == "00102322000141");

        boreal.FundRegistryId.Should().Be(27771);
        boreal.ClassType.Should().Be("Classes de Cotas de Fundos FIF");
    }

    private static List<ParsedCdaRow> ParseMonthFile(string fileName)
    {
        var csv = Fixtures.Open(Fixtures.MonthFolder, fileName);
        var parser = CdaFileParsers.For(fileName, September)!;
        var guard = new RowGuard(fileName);

        return csv.Rows.Select(row =>
        {
            var (origin, rejection) = guard.Inspect(row);
            return rejection is null ? parser.Parse(row, origin) : ParsedCdaRow.Rejected(rejection);
        }).ToList();
    }
}
