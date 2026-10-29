using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Myc.Core.Inspect;

/// <summary>A decoded token, or a reason the text is not one.</summary>
public sealed record JwtInspection(bool Ok, string Text, string? Error)
{
    public static JwtInspection Ready(string text) => new(true, text, null);

    public static JwtInspection Refuse(string error) => new(false, "", error);
}

/// <summary>
/// Decodes a compact JWT. The signature is not checked, and an encrypted token is not opened.
/// </summary>
public static class JwtInspector
{
    public const int MaxChars = 64 * 1024;

    public static bool LooksLike(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxChars || HasWhitespace(trimmed))
        {
            return false;
        }

        string[] parts = trimmed.Split('.');
        if (parts.Length == 5)
        {
            return true;
        }

        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        if (!TryDecode(parts[0], out byte[] header))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(header);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("alg", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static JwtInspection Inspect(string token, DateTimeOffset now, TimeZoneInfo zone)
    {
        string trimmed = token.Trim();
        if (trimmed.Length > MaxChars)
        {
            return JwtInspection.Refuse("That token is too long.");
        }

        if (trimmed.Length == 0 || HasWhitespace(trimmed))
        {
            return JwtInspection.Refuse("This is not a JWT.");
        }

        string[] parts = trimmed.Split('.');
        if (parts.Length == 5)
        {
            return JwtInspection.Ready("This token is encrypted.");
        }

        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || !TryDecode(parts[0], out byte[] headerBytes))
        {
            return JwtInspection.Refuse("This is not a JWT.");
        }

        if (!TryDecode(parts[1], out byte[] payloadBytes))
        {
            return JwtInspection.Refuse("This is not a JWT.");
        }

        JsonDocument header;
        JsonDocument payload;
        try
        {
            header = JsonDocument.Parse(headerBytes);
            payload = JsonDocument.Parse(payloadBytes);
        }
        catch (JsonException)
        {
            return JwtInspection.Refuse("This is not a JWT.");
        }

        using (header)
        using (payload)
        {
            if (header.RootElement.ValueKind != JsonValueKind.Object)
            {
                return JwtInspection.Refuse("This is not a JWT.");
            }

            string? algorithm = null;
            if (header.RootElement.TryGetProperty("alg", out JsonElement alg) && alg.ValueKind == JsonValueKind.String)
            {
                algorithm = alg.GetString();
            }

            var builder = new StringBuilder();
            builder.AppendLine("Header");
            builder.AppendLine(Pretty(header));
            builder.AppendLine();
            builder.AppendLine("Payload");
            builder.AppendLine(Pretty(payload));
            if (payload.RootElement.ValueKind == JsonValueKind.Object)
            {
                AppendTimes(builder, payload.RootElement, now, zone);
            }

            builder.AppendLine();
            builder.Append(string.Equals(algorithm, "none", StringComparison.Ordinal)
                ? "Algorithm is none. The signature was not checked."
                : "Signature present. Not checked.");
            return JwtInspection.Ready(builder.ToString());
        }
    }

    private static void AppendTimes(StringBuilder builder, JsonElement payload, DateTimeOffset now, TimeZoneInfo zone)
    {
        bool any = false;
        any |= AppendTime(builder, payload, "exp", now, zone, past: "expired ", future: "expires in ", suffixPast: " ago", suffixFuture: "");
        any |= AppendTime(builder, payload, "nbf", now, zone, past: null, future: "not valid for ", suffixPast: null, suffixFuture: "");
        any |= AppendTime(builder, payload, "iat", now, zone, past: "issued ", future: null, suffixPast: " ago", suffixFuture: null);
        if (any)
        {
            builder.AppendLine();
        }
    }

    private static bool AppendTime(
        StringBuilder builder,
        JsonElement payload,
        string name,
        DateTimeOffset now,
        TimeZoneInfo zone,
        string? past,
        string? future,
        string? suffixPast,
        string? suffixFuture)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Number || !TryUnix(value, out long unix))
        {
            return false;
        }

        DateTimeOffset instant = DateTimeOffset.FromUnixTimeSeconds(unix);
        builder.AppendLine();
        builder.Append(name.PadRight(12));
        builder.Append(Local(instant, zone));
        bool before = instant <= now;
        string? phrase = before ? past : future;
        string? suffix = before ? suffixPast : suffixFuture;
        if (phrase is not null && suffix is not null)
        {
            builder.Append("   ");
            builder.Append(phrase);
            builder.Append(Quantity(instant, now));
            builder.Append(suffix);
        }

        return true;
    }

    private static bool TryUnix(JsonElement value, out long unix)
    {
        if (value.TryGetInt64(out unix))
        {
            return true;
        }

        if (value.TryGetDouble(out double number))
        {
            unix = (long)number;
            return true;
        }

        unix = 0;
        return false;
    }

    private static string Quantity(DateTimeOffset instant, DateTimeOffset now)
    {
        TimeSpan span = (instant - now).Duration();
        if (span.TotalMinutes < 90)
        {
            int minutes = Math.Max(1, (int)span.TotalMinutes);
            return minutes == 1 ? "1 minute" : minutes + " minutes";
        }

        if (span.TotalHours < 24)
        {
            int hours = Math.Max(1, (int)span.TotalHours);
            return hours == 1 ? "1 hour" : hours + " hours";
        }

        int days = Math.Max(1, (int)span.TotalDays);
        return days == 1 ? "1 day" : days + " days";
    }

    private static string Local(DateTimeOffset instant, TimeZoneInfo zone)
    {
        DateTimeOffset shown = TimeZoneInfo.ConvertTime(instant, zone);
        string clock = shown.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (shown.Offset == TimeSpan.Zero)
        {
            return clock + " UTC";
        }

        string sign = shown.Offset < TimeSpan.Zero ? "-" : "+";
        return clock + " UTC" + sign + shown.Offset.Duration().ToString("hh\\:mm", CultureInfo.InvariantCulture);
    }

    private static string Pretty(JsonDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool TryDecode(string segment, out byte[] bytes)
    {
        bytes = [];
        if (segment.Length == 0)
        {
            return false;
        }

        string padded = segment.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 0:
                break;
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            default:
                return false;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasWhitespace(string text)
    {
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }
}
