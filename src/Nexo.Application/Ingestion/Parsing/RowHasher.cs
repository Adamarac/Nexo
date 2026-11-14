using System.Security.Cryptography;
using System.Text;

namespace Nexo.Application.Ingestion.Parsing;

/// <summary>Gera um código curto e único para o conteúdo de uma linha.</summary>
internal static class RowHasher
{
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
