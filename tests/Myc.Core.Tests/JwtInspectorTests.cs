using System.Text;
using Myc.Core.Inspect;

namespace Myc.Core.Tests;

public class JwtInspectorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void Header_and_payload_are_decoded()
    {
        string token = Compact("{\"alg\":\"HS256\",\"typ\":\"JWT\"}", "{\"sub\":\"ada\"}");

        JwtInspection inspection = JwtInspector.Inspect(token, DateTimeOffset.UnixEpoch, Utc);

        Assert.True(inspection.Ok);
        Assert.Contains("\"alg\": \"HS256\"", inspection.Text);
        Assert.Contains("\"sub\": \"ada\"", inspection.Text);
        Assert.Contains("Signature present. Not checked.", inspection.Text);
        Assert.True(JwtInspector.LooksLike(token));
    }

    [Fact]
    public void Expiry_is_a_local_time_and_a_phrase()
    {
        var exp = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        string token = Compact("{\"alg\":\"HS256\"}", "{\"exp\":" + exp.ToUnixTimeSeconds() + "}");

        JwtInspection past = JwtInspector.Inspect(token, exp.AddDays(3).AddHours(1), Utc);
        JwtInspection future = JwtInspector.Inspect(token, exp.AddHours(-5), Utc);

        Assert.Contains("2024-06-01 12:00 UTC", past.Text);
        Assert.Contains("expired 3 days ago", past.Text);
        Assert.Contains("expires in 5 hours", future.Text);
    }

    [Fact]
    public void Not_before_and_issued_at_get_phrases()
    {
        var nbf = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var iat = nbf.AddDays(-2);
        string token = Compact(
            "{\"alg\":\"HS256\"}",
            "{\"nbf\":" + nbf.ToUnixTimeSeconds() + ",\"iat\":" + iat.ToUnixTimeSeconds() + "}");

        JwtInspection inspection = JwtInspector.Inspect(token, nbf.AddDays(-1), Utc);

        Assert.Contains("not valid for 1 day", inspection.Text);
        Assert.Contains("issued 1 day ago", inspection.Text);
    }

    [Fact]
    public void None_is_called_out()
    {
        string token = B64("{\"alg\":\"none\"}") + "." + B64("{\"sub\":\"ada\"}") + ".";

        JwtInspection inspection = JwtInspector.Inspect(token, DateTimeOffset.UnixEpoch, Utc);

        Assert.True(inspection.Ok);
        Assert.Contains("Algorithm is none. The signature was not checked.", inspection.Text);
        Assert.True(JwtInspector.LooksLike(token));
    }

    [Fact]
    public void Five_segments_are_encrypted()
    {
        string token = string.Join('.', Enumerable.Repeat(B64("x"), 5));

        JwtInspection inspection = JwtInspector.Inspect(token, DateTimeOffset.UnixEpoch, Utc);

        Assert.True(inspection.Ok);
        Assert.Equal("This token is encrypted.", inspection.Text);
        Assert.True(JwtInspector.LooksLike(token));
    }

    [Fact]
    public void Ordinary_text_is_refused()
    {
        JwtInspection inspection = JwtInspector.Inspect("not a token", DateTimeOffset.UnixEpoch, Utc);

        Assert.False(inspection.Ok);
        Assert.Equal("This is not a JWT.", inspection.Error);
        Assert.False(JwtInspector.LooksLike("not a token"));
    }

    [Fact]
    public void A_long_token_is_refused()
    {
        string token = new string('a', JwtInspector.MaxChars + 1);

        JwtInspection inspection = JwtInspector.Inspect(token, DateTimeOffset.UnixEpoch, Utc);

        Assert.False(inspection.Ok);
        Assert.Equal("That token is too long.", inspection.Error);
    }

    private static string Compact(string header, string payload) =>
        B64(header) + "." + B64(payload) + "." + B64("sig");

    private static string B64(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
