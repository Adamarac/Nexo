using Nexo.Domain.Common;

namespace Nexo.UnitTests.Domain;

public sealed class ReferenceMonthTests
{
    [Theory]
    [InlineData("cda_fi_202609", 2026, 9)]
    [InlineData("cda_fi_BLC_4_202609.csv", 2026, 9)]
    [InlineData("cda_fie_CONFID_202301.csv", 2023, 1)]
    public void TryFromFileName_ReadsYearAndMonth(string fileName, int year, int month)
    {
        ReferenceMonth.TryFromFileName(fileName, out var reference).Should().BeTrue();
        reference.Should().Be(new ReferenceMonth(year, month));
    }

    [Theory]
    [InlineData("registro_fundo_classe.zip")]
    [InlineData("cda_fi_202613.zip")]
    public void TryFromFileName_FailsWithoutValidMonth(string fileName)
    {
        ReferenceMonth.TryFromFileName(fileName, out _).Should().BeFalse();
    }

    [Fact]
    public void Contains_IsTrueOnlyForDatesInsideTheMonth()
    {
        var september = new ReferenceMonth(2026, 9);

        september.Contains(new DateOnly(2026, 9, 30)).Should().BeTrue();
        september.Contains(new DateOnly(2026, 8, 31)).Should().BeFalse();
        september.LastDay.Should().Be(new DateOnly(2026, 9, 30));
    }
}
