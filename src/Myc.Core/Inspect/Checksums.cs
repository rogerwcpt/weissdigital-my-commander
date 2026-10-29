using System.Security.Cryptography;
using System.Text;

namespace Myc.Core.Inspect;

/// <summary>SHA-256 of a file, and the page that lists a selection.</summary>
public static class Checksums
{
    public readonly record struct Row(string Name, string? Hex, string? Error);

    public static string Sha256(Stream stream)
    {
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Format(IReadOnlyList<Row> rows)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(rows[i].Name);
            builder.AppendLine(rows[i].Hex ?? rows[i].Error ?? "");
        }

        return builder.ToString().TrimEnd();
    }
}
