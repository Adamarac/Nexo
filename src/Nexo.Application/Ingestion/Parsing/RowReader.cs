using System.Globalization;
using Nexo.Domain.Common;
using Nexo.Domain.Ingestion;

namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Lê os campos de uma linha convertendo tipos e guardando o primeiro problema encontrado.</summary>
/// <remarks>Em vez de lançar exceção a cada campo ruim, a linha inteira vira uma rejeição com motivo.</remarks>
internal sealed class RowReader(CsvRow row)
{
    private const NumberStyles NumberFormat =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    public CsvRow Row { get; } = row;

    public RowRejection? Rejection { get; private set; }

    public bool HasFailed => Rejection is not null;

    public string? Text(string column) => Row[column];

    public string RequiredText(string column)
    {
        var value = Row[column];

        if (value is null)
            Reject(RejectionReason.MissingRequiredValue, $"A coluna {column} está vazia.");

        return value ?? string.Empty;
    }

    public decimal? Decimal(string column)
    {
        var value = Row[column];

        if (value is null)
            return null;

        if (decimal.TryParse(value, NumberFormat, CultureInfo.InvariantCulture, out var number))
            return number;

        Reject(RejectionReason.InvalidNumber, $"O valor '{value}' da coluna {column} não é um número.");
        return null;
    }

    public long RequiredLong(string column)
    {
        var value = Row[column];

        if (value is null)
        {
            Reject(RejectionReason.MissingRequiredValue, $"A coluna {column} está vazia.");
            return 0;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return number;

        Reject(RejectionReason.InvalidNumber, $"O valor '{value}' da coluna {column} não é um número inteiro.");
        return 0;
    }

    public DateOnly? Date(string column)
    {
        var value = Row[column];

        if (value is null)
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        Reject(RejectionReason.InvalidDate, $"A data '{value}' da coluna {column} não está no formato AAAA-MM-DD.");
        return null;
    }

    public DateOnly RequiredDate(string column)
    {
        if (Row[column] is null)
        {
            Reject(RejectionReason.MissingRequiredValue, $"A coluna {column} está vazia.");
            return default;
        }

        return Date(column) ?? default;
    }

    public Cnpj RequiredCnpj(string column)
    {
        var value = Row[column];

        if (value is null)
        {
            Reject(RejectionReason.MissingRequiredValue, $"A coluna {column} está vazia.");
            return default;
        }

        if (Cnpj.TryParse(value, out var cnpj))
            return cnpj;

        Reject(RejectionReason.InvalidCnpj, $"O CNPJ '{value}' da coluna {column} é inválido.");
        return default;
    }

    // A CVM usa "S" e "N". Qualquer outra coisa conta como não informado.
    public bool? YesNo(string column) => Row[column] switch
    {
        "S" => true,
        "N" => false,
        _ => null,
    };

    public void Reject(RejectionReason reason, string detail) =>
        Rejection ??= new RowRejection(Row.LineNumber, reason, detail, Row.Raw);
}
