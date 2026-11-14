using Nexo.Domain.Common;

namespace Nexo.UnitTests.Domain;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Ação ordinária", "ACAO ORDINARIA")]
    [InlineData("  APPLE        DRN ED  ", "APPLE DRN ED")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Normalize_UppercasesRemovesAccentsAndCollapsesSpaces(string? input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("01.599.101/0001-93", "01599101000193")]
    [InlineData("26845679000103", "26845679000103")]
    [InlineData("00000000000000", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeDocument_KeepsOnlyLettersAndDigits(string? input, string? expected)
    {
        TextNormalizer.NormalizeDocument(input).Should().Be(expected);
    }
}
