using Nexo.Domain.Common;

namespace Nexo.UnitTests.Domain;

public sealed class CnpjTests
{
    [Theory]
    [InlineData("00.017.024/0001-53")]
    [InlineData("00017024000153")]
    [InlineData(" 00017024000153 ")]
    public void TryParse_AcceptsValueWithOrWithoutPunctuation(string raw)
    {
        Cnpj.TryParse(raw, out var cnpj).Should().BeTrue();
        cnpj.Value.Should().Be("00017024000153");
    }

    [Theory]
    [InlineData("00.017.024/0001-54")]
    [InlineData("0001702400015")]
    [InlineData("00000000000000")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ABC")]
    public void TryParse_RejectsInvalidValues(string? raw)
    {
        Cnpj.TryParse(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_AcceptsAlphanumericCnpj()
    {
        Cnpj.TryParse("12.ABC.345/01DE-35", out var cnpj).Should().BeTrue();
        cnpj.Value.Should().Be("12ABC34501DE35");
    }

    [Fact]
    public void Formatted_AddsPunctuation()
    {
        Cnpj.Parse("00017024000153").Formatted.Should().Be("00.017.024/0001-53");
    }

    [Fact]
    public void Parse_ThrowsForInvalidValue()
    {
        var act = () => Cnpj.Parse("123");

        act.Should().Throw<FormatException>();
    }
}
