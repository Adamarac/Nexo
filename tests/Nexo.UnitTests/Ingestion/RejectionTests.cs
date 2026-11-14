using Nexo.Application.Ingestion.Parsing;
using Nexo.Application.Ingestion.Parsing.Cda;
using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;
using Nexo.UnitTests.Support;

namespace Nexo.UnitTests.Ingestion;

/// <summary>Linhas ruins viram rejeições com motivo, nunca somem em silêncio.</summary>
public sealed class RejectionTests
{
    private const string Block8Header =
        "TP_FUNDO_CLASSE;CNPJ_FUNDO_CLASSE;DENOM_SOCIAL;DT_COMPTC;TP_APLIC;TP_ATIVO;EMISSOR_LIGADO;TP_NEGOC;QT_VENDA_NEGOC;VL_VENDA_NEGOC;QT_AQUIS_NEGOC;VL_AQUIS_NEGOC;QT_POS_FINAL;VL_MERC_POS_FINAL;VL_CUSTO_POS_FINAL;DT_CONFID_APLIC;DS_ATIVO;PF_PJ_EMISSOR;CPF_CNPJ_EMISSOR;EMISSOR";

    private const string ValidBlock8Row =
        "CLASSES - FIF;00.017.024/0001-53;FUNDO TESTE;2026-09-30;Disponibilidades;Outros;;Para negociação;;;;;;110441.45;;;Disponibilidade;;;";

    private static readonly ReferenceMonth September = new(2026, 9);

    [Fact]
    public void ValidRow_IsAccepted()
    {
        Parse(ValidBlock8Row).Rejection.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-09-31", RejectionReason.InvalidDate)]
    [InlineData("30/09/2026", RejectionReason.InvalidDate)]
    [InlineData("2026-08-31", RejectionReason.ReferenceDateMismatch)]
    [InlineData("", RejectionReason.MissingRequiredValue)]
    public void BadReferenceDate_IsRejected(string date, RejectionReason expected)
    {
        Parse(ValidBlock8Row.Replace("2026-09-30", date, StringComparison.Ordinal)).Rejection!.Reason.Should().Be(expected);
    }

    [Fact]
    public void NumberWithCommaAsDecimalSeparator_IsRejected()
    {
        var rejection = Parse(ValidBlock8Row.Replace("110441.45", "110441,45", StringComparison.Ordinal)).Rejection!;

        rejection.Reason.Should().Be(RejectionReason.InvalidNumber);
        rejection.Detail.Should().Contain("VL_MERC_POS_FINAL");
    }

    [Fact]
    public void InvalidCnpj_IsRejected()
    {
        Parse(ValidBlock8Row.Replace("00.017.024/0001-53", "00.017.024/0001-54", StringComparison.Ordinal))
            .Rejection!.Reason.Should().Be(RejectionReason.InvalidCnpj);
    }

    [Fact]
    public void MissingApplicationType_IsRejected()
    {
        Parse(ValidBlock8Row.Replace("Disponibilidades", "", StringComparison.Ordinal))
            .Rejection!.Reason.Should().Be(RejectionReason.MissingRequiredValue);
    }

    [Fact]
    public void RowWithMissingFields_IsRejectedBeforeParsing()
    {
        var row = CsvText.SingleRow("cda_fi_BLC_8_202609.csv", Block8Header, "CLASSES - FIF;00.017.024/0001-53;FUNDO");

        new RowGuard("cda_fi_BLC_8_202609.csv").Inspect(row).Rejection!.Reason.Should().Be(RejectionReason.FieldCountMismatch);
    }

    [Fact]
    public void RepeatedRow_IsRejectedAsDuplicate()
    {
        var rows = CsvText.Parse("cda_fi_BLC_8_202609.csv", Block8Header, ValidBlock8Row, ValidBlock8Row).Rows.ToList();
        var guard = new RowGuard("cda_fi_BLC_8_202609.csv");

        guard.Inspect(rows[0]).Rejection.Should().BeNull();
        guard.Inspect(rows[1]).Rejection!.Reason.Should().Be(RejectionReason.DuplicateRow);
    }

    [Fact]
    public void EmptyQuantity_IsNotTheSameAsZero()
    {
        var position = Parse(ValidBlock8Row).Position!;

        position.Amounts.Quantity.Should().BeNull();
        position.Amounts.MarketValue.Should().Be(110441.45m);
    }

    [Fact]
    public void StructuredFundWithTwoDifferentNetWorths_RejectsTheSecondRow()
    {
        const string header = "TP_FUNDO_CLASSE;CNPJ_FUNDO_CLASSE;DENOM_SOCIAL;DT_COMPTC;ID_DOC;VL_PATRIM_LIQ;TP_APLIC;TP_ATIVO;VL_MERC_POS_FINAL";
        var rows = CsvText.Parse("cda_fie_202609.csv", header,
            "CLASSES - FIDC;00.017.024/0001-53;FIDC X;2026-09-30;1;100.00;Disponibilidades;Outros;10.00",
            "CLASSES - FIDC;00.017.024/0001-53;FIDC X;2026-09-30;1;200.00;Valores a pagar;Outros;-5.00").Rows.ToList();
        var parser = new StructuredFileParser(September);

        parser.Parse(rows[0], Origin(rows[0])).NetWorth!.NetWorth.Should().Be(100m);
        parser.Parse(rows[1], Origin(rows[1])).Rejection!.Reason.Should().Be(RejectionReason.InconsistentNetWorth);
    }

    [Fact]
    public void FileFrom2023_IsReadWithTheOldColumnNames()
    {
        var row = CsvText.SingleRow("cda_fi_BLC_2_202301.csv",
            "TP_FUNDO;CNPJ_FUNDO;DENOM_SOCIAL;DT_COMPTC;TP_APLIC;TP_ATIVO;EMISSOR_LIGADO;TP_NEGOC;QT_VENDA_NEGOC;VL_VENDA_NEGOC;QT_AQUIS_NEGOC;VL_AQUIS_NEGOC;QT_POS_FINAL;VL_MERC_POS_FINAL;VL_CUSTO_POS_FINAL;DT_CONFID_APLIC;CNPJ_FUNDO_COTA;NM_FUNDO_COTA",
            "FAPI;02.010.153/0001-45;BB FAPI;2023-01-31;Cotas de Fundos;Fundos de Índice;N;;;;;;400.000000;30640.00;;;13.416.245/0001-46;IT NOW IDIV FUNDO DE ÍNDICE");
        var parser = CdaFileParsers.For("cda_fi_BLC_2_202301.csv", new ReferenceMonth(2023, 1))!;

        var position = parser.Parse(row, Origin(row)).Position!;

        position.Fund.Cnpj.Value.Should().Be("02010153000145");
        position.Fund.FundType.Should().Be("FAPI");
        position.Asset!.Identity.Key.Should().Be("fund:13416245000146");
    }

    private static ParsedCdaRow Parse(string line)
    {
        var row = CsvText.SingleRow("cda_fi_BLC_8_202609.csv", Block8Header, line);
        return CdaFileParsers.For("cda_fi_BLC_8_202609.csv", September)!.Parse(row, Origin(row));
    }

    private static RowOrigin Origin(CsvRow row) => new("teste.csv", row.LineNumber, RowHasher.Hash(row.Raw));
}
