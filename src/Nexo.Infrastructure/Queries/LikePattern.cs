namespace Nexo.Infrastructure.Queries;

internal static class LikePattern
{
    // Escapa % e _ para o texto digitado ser procurado do jeito que está, e não como curinga.
    public static string Contains(string text)
    {
        var escaped = text.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
