using Nexo.Domain.Assets;

namespace Nexo.UnitTests.Domain;

public sealed class AssetIdentityTests
{
    [Fact]
    public void SameAssetWithDifferentSpacing_HasTheSameKey()
    {
        var first = AssetIdentity.ForOther("Disponibilidades", "Outros", "Disponibilidade");
        var second = AssetIdentity.ForOther("disponibilidades ", "OUTROS", "  disponibilidade");

        first.Should().Be(second);
        first.Key.Should().Be("other:DISPONIBILIDADES:OUTROS:DISPONIBILIDADE");
    }

    [Fact]
    public void FundShare_UsesCnpjAndSubclassWhenPresent()
    {
        AssetIdentity.ForFundShare("01165780000192", null).Key.Should().Be("fund:01165780000192");
        AssetIdentity.ForFundShare("67673651000113", "B8N491782435172").Key
            .Should().Be("fund:67673651000113:B8N491782435172");
    }

    [Fact]
    public void BankDeposit_CombinesIssuerTypeMaturityAndIndexer()
    {
        var identity = AssetIdentity.ForBankDeposit("00416968000101", "CDB/ RDB", new DateOnly(2027, 2, 24), "DI1");

        identity.Key.Should().Be("bank:00416968000101:CDB/ RDB:2027-02-24:DI1");
    }

    [Fact]
    public void Isin_IsUppercasedAndTrimmed()
    {
        AssetIdentity.ForIsin(" britubacnor4 ").Key.Should().Be("isin:BRITUBACNOR4");
    }
}
