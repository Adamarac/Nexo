using System.Globalization;
using System.Text;

namespace Nexo.Domain.Common;

public static class TextNormalizer
{
    private const string UnknownDocument = "00000000000000";

    // A mesma descrição chega com acentos, caixa e espaços diferentes entre fundos.
    // Normalizar evita que um mesmo ativo vire vários registros.
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var withoutAccents = RemoveAccents(text.Trim().ToUpperInvariant());
        return string.Join(' ', withoutAccents.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // A CVM usa 14 zeros quando não sabe quem é o emissor, então tratamos como "sem documento".
    public static string? NormalizeDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
            return null;

        var cleaned = new string(document.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return cleaned.Length == 0 || cleaned == UnknownDocument ? null : cleaned;
    }

    private static string RemoveAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
