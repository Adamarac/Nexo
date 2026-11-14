using System.Text;
using Nexo.Infrastructure.Cvm;
using Nexo.UnitTests.Support;

namespace Nexo.UnitTests.Ingestion;

public sealed class CvmCsvReaderTests
{
    [Fact]
    public void Open_DecodesLatin1Text()
    {
        var bytes = Encoding.Latin1.GetBytes("DENOM_SOCIAL;DT_COMPTC\r\nBOREAL AÇÕES III;2026-09-30\r\n");
        using var reader = new StreamReader(new MemoryStream(bytes), CvmCsvReader.Encoding);

        var row = CvmCsvReader.Open(reader, "x.csv").Rows.Single();

        row["DENOM_SOCIAL"].Should().Be("BOREAL AÇÕES III");
    }

    [Fact]
    public void Open_TreatsQuotesAsPlainText()
    {
        var row = CsvText.SingleRow("cad.csv",
            "NOME;TAXA;SITUACAO",
            "FUNDO X;FUNDOS \"REFERENCIADO DI\" E \"CURTO PRAZO;ATIVO");

        row.Fields.Should().HaveCount(3);
        row["TAXA"].Should().Be("FUNDOS \"REFERENCIADO DI\" E \"CURTO PRAZO");
        row["SITUACAO"].Should().Be("ATIVO");
    }

    [Fact]
    public void Open_NumbersLinesFromTheHeaderAndSkipsBlankLines()
    {
        var rows = CsvText.Parse("x.csv", "A;B", "1;2", "", "3;4").Rows.ToList();

        rows.Select(row => row.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Indexer_ReturnsNullForEmptyOrMissingColumns()
    {
        var row = CsvText.SingleRow("x.csv", "A;B", " ;2");

        row["A"].Should().BeNull();
        row["C"].Should().BeNull();
        row["B"].Should().Be("2");
    }
}
