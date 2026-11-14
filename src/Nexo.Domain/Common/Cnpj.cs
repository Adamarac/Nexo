namespace Nexo.Domain.Common;

/// <summary>CNPJ normalizado, com 14 caracteres e dígitos verificadores conferidos.</summary>
public readonly record struct Cnpj
{
    private const int Length = 14;
    private static readonly int[] FirstDigitWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondDigitWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cnpj(string value) => Value = value;

    public string Value { get; }

    public string Formatted => $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    // A CVM manda o CNPJ com pontuação em alguns arquivos e sem em outros.
    // Guardamos só letras e dígitos para o mesmo fundo nunca virar dois registros.
    public static bool TryParse(string? raw, out Cnpj cnpj)
    {
        cnpj = default;
        var normalized = TextNormalizer.NormalizeDocument(raw);

        if (normalized is null || !HasValidFormat(normalized) || !HasValidCheckDigits(normalized))
            return false;

        cnpj = new Cnpj(normalized);
        return true;
    }

    public static Cnpj Parse(string raw) =>
        TryParse(raw, out var cnpj) ? cnpj : throw new FormatException($"CNPJ inválido: '{raw}'.");

    // Usado só ao ler do banco, onde o valor já foi validado na entrada.
    public static Cnpj FromTrusted(string value) => new(value);

    public override string ToString() => Value;

    // Desde 2026 a Receita emite CNPJ com letras nas 12 primeiras posições.
    // Os dois últimos caracteres continuam sendo dígitos.
    private static bool HasValidFormat(string value) =>
        value.Length == Length
        && value[..12].All(char.IsAsciiLetterOrDigit)
        && value[12..].All(char.IsAsciiDigit);

    private static bool HasValidCheckDigits(string value) =>
        CheckDigit(value[..12], FirstDigitWeights) == value[12] - '0'
        && CheckDigit(value[..13], SecondDigitWeights) == value[13] - '0';

    // Cada caractere vale o seu código menos 48, o que dá 0 a 9 para dígitos e 17 a 42 para letras.
    private static int CheckDigit(string digits, int[] weights)
    {
        var sum = digits.Select((character, index) => (character - '0') * weights[index]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
