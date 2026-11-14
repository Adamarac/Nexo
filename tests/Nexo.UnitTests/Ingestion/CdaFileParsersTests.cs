using Nexo.Application.Ingestion.Parsing.Cda;
using Nexo.Domain.Common;

namespace Nexo.UnitTests.Ingestion;

public sealed class CdaFileParsersTests
{
    private static readonly ReferenceMonth September = new(2026, 9);

    [Theory]
    [InlineData("cda_fi_PL_202609.csv", typeof(NetWorthFileParser))]
    [InlineData("cda_fi_BLC_1_202609.csv", typeof(BlockFileParser))]
    [InlineData("cda_fi_BLC_8_202609.csv", typeof(BlockFileParser))]
    [InlineData("cda_fi_CONFID_202609.csv", typeof(ConfidentialFileParser))]
    [InlineData("cda_fie_CONFID_202609.csv", typeof(ConfidentialFileParser))]
    [InlineData("cda_fie_202609.csv", typeof(StructuredFileParser))]
    [InlineData("cda_fiim_202301.csv", typeof(StructuredFileParser))]
    public void For_PicksTheParserByFileName(string fileName, Type expected)
    {
        CdaFileParsers.For(fileName, September).Should().BeOfType(expected);
    }

    [Theory]
    [InlineData("cda_fi_BLC_9_202609.csv")]
    [InlineData("leia_me.csv")]
    public void For_ReturnsNullForUnknownFiles(string fileName)
    {
        CdaFileParsers.For(fileName, September).Should().BeNull();
    }
}
