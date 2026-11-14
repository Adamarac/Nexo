using Nexo.Application.Ingestion.Parsing;

namespace Nexo.UnitTests.Ingestion;

public sealed class CsvHeaderTests
{
    [Fact]
    public void IndexOf_FindsColumnsByTheirOldNames()
    {
        var header = new CsvHeader(["TP_FUNDO", "CNPJ_FUNDO", "CNPJ_FUNDO_COTA", "NM_FUNDO_COTA"]);

        header.IndexOf("TP_FUNDO_CLASSE").Should().Be(0);
        header.IndexOf("CNPJ_FUNDO_CLASSE").Should().Be(1);
        header.IndexOf("CNPJ_FUNDO_CLASSE_COTA").Should().Be(2);
        header.IndexOf("NM_FUNDO_CLASSE_SUBCLASSE_COTA").Should().Be(3);
    }

    [Fact]
    public void MissingColumns_ListsOnlyWhatIsAbsent()
    {
        var header = new CsvHeader(["CNPJ_FUNDO_CLASSE", "DT_COMPTC"]);

        header.MissingColumns(["CNPJ_FUNDO_CLASSE", "DT_COMPTC", "TP_APLIC"]).Should().Equal("TP_APLIC");
    }
}
