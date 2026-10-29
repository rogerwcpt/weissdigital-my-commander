using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Myc.Core.Inspect;

namespace Myc.Core.Tests;

public class CertificateInspectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pem_certificate_shows_the_subject_names_and_fingerprint()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "example.com", Now.AddDays(-1), Now.AddDays(10), ca: false, "example.com", "www.example.com");
        byte[] pem = Encoding.UTF8.GetBytes(cert.ExportCertificatePem());

        CertificateInspection inspection = CertificateInspector.Inspect(pem, password: null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("CN=example.com", inspection.Text);
        Assert.Contains("example.com, www.example.com", inspection.Text);
        Assert.Contains("Valid for 10 days", inspection.Text);
        Assert.Contains(Fingerprint(cert.RawData), inspection.Text);
        Assert.Contains("CA          No", inspection.Text);
        Assert.Contains("Digital signature", inspection.Text);
        Assert.DoesNotContain("PRIVATE KEY", inspection.Text);
    }

    [Fact]
    public void Der_certificate_reads_the_same_subject()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "der.example", Now.AddDays(-1), Now.AddDays(30), ca: false);
        CertificateInspection inspection = CertificateInspector.Inspect(cert.RawData, null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("CN=der.example", inspection.Text);
        Assert.Contains("Valid for 30 days", inspection.Text);
    }

    [Fact]
    public void A_chain_is_shown_leaf_first()
    {
        using var rootKey = RSA.Create(2048);
        using X509Certificate2 root = SelfSigned(rootKey, "root.example", Now.AddDays(-1), Now.AddDays(30), ca: true);
        using var leafKey = RSA.Create(2048);
        using X509Certificate2 leaf = Issued(leafKey, root, "leaf.example", Now.AddDays(-1), Now.AddDays(30));
        string pem = root.ExportCertificatePem() + leaf.ExportCertificatePem();

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(pem), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("2 certificates", inspection.Text);
        int leafAt = inspection.Text.IndexOf("CN=leaf.example", StringComparison.Ordinal);
        int rootAt = inspection.Text.IndexOf("CN=root.example", StringComparison.Ordinal);
        Assert.True(leafAt >= 0 && rootAt > leafAt);
    }

    [Fact]
    public void Pkcs7_shows_the_certificate()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "bag.example", Now.AddDays(-1), Now.AddDays(5), ca: false);
        string pkcs7 = new X509Certificate2Collection(cert).ExportPkcs7Pem();

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(pkcs7), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("CN=bag.example", inspection.Text);
    }

    [Fact]
    public void Pkcs12_asks_for_a_password_and_hides_the_key()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "bag.example", Now.AddDays(-2), Now.AddMinutes(-1), ca: false);
        byte[] pfx = cert.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, "secret");
        string keyPem = rsa.ExportPkcs8PrivateKeyPem();

        CertificateInspection locked = CertificateInspector.Inspect(pfx, null, Now);
        CertificateInspection wrong = CertificateInspector.Inspect(pfx, "nope", Now);
        CertificateInspection opened = CertificateInspector.Inspect(pfx, "secret", Now);

        Assert.Equal(CertificateStatus.NeedsPassword, locked.Status);
        Assert.Null(locked.Error);
        Assert.Equal(CertificateStatus.NeedsPassword, wrong.Status);
        Assert.Equal("Wrong password.", wrong.Error);
        Assert.Equal(CertificateStatus.Ready, opened.Status);
        Assert.Contains("PKCS#12", opened.Text);
        Assert.Contains("Private key Yes", opened.Text);
        Assert.Contains("CN=bag.example", opened.Text);
        Assert.Contains("Expired", opened.Text);
        Assert.DoesNotContain("BEGIN", opened.Text);
        Assert.DoesNotContain(FirstBodyLine(keyPem), opened.Text);
    }

    [Fact]
    public void An_empty_password_is_tried_before_asking()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "open.example", Now.AddDays(-1), Now.AddDays(3), ca: false);
        byte[] pfx = cert.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, "");

        CertificateInspection inspection = CertificateInspector.Inspect(pfx, null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("CN=open.example", inspection.Text);
    }

    [Fact]
    public void A_request_is_not_described_as_a_certificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=asked.example", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("asked.example");
        request.CertificateExtensions.Add(names.Build());

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(request.CreateSigningRequestPem()), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.StartsWith("Certificate request", inspection.Text);
        Assert.Contains("CN=asked.example", inspection.Text);
        Assert.Contains("asked.example", inspection.Text);
        Assert.DoesNotContain("Valid to", inspection.Text);
        Assert.DoesNotContain("Issuer", inspection.Text);
    }

    [Fact]
    public void A_private_key_is_one_line()
    {
        using var rsa = RSA.Create(2048);
        string pem = rsa.ExportPkcs8PrivateKeyPem();

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(pem), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Equal("Private key. Not shown.", inspection.Text);
    }

    [Fact]
    public void A_certificate_beside_a_key_names_the_key_and_does_not_print_it()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "both.example", Now.AddDays(-1), Now.AddDays(4), ca: false);
        string key = rsa.ExportPkcs8PrivateKeyPem();
        string pem = cert.ExportCertificatePem() + key;

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(pem), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("Private key Yes", inspection.Text);
        Assert.Contains("CN=both.example", inspection.Text);
        Assert.DoesNotContain(FirstBodyLine(key), inspection.Text);
    }

    [Fact]
    public void A_public_key_shows_the_algorithm_and_size()
    {
        using var rsa = RSA.Create(2048);
        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()), null, Now);

        Assert.Equal(CertificateStatus.Ready, inspection.Status);
        Assert.Contains("RSA 2048", inspection.Text);
    }

    [Fact]
    public void A_certificate_that_is_not_valid_yet_says_so()
    {
        using var rsa = RSA.Create(2048);
        using X509Certificate2 cert = SelfSigned(rsa, "later.example", Now.AddDays(2), Now.AddDays(10), ca: false);

        CertificateInspection inspection = CertificateInspector.Inspect(Encoding.UTF8.GetBytes(cert.ExportCertificatePem()), null, Now);

        Assert.Contains("Not valid yet", inspection.Text);
    }

    [Fact]
    public void Random_bytes_are_refused()
    {
        CertificateInspection inspection = CertificateInspector.Inspect([1, 2, 3, 4, 5], null, Now);

        Assert.Equal(CertificateStatus.Refused, inspection.Status);
        Assert.Equal("This is not a certificate.", inspection.Error);
    }

    [Theory]
    [InlineData("site.pem", true)]
    [InlineData("site.PFX", true)]
    [InlineData("site.crt", true)]
    [InlineData("notes.md", false)]
    public void Names_that_look_like_certificates(string name, bool expected)
    {
        Assert.Equal(expected, CertificateInspector.LooksLikeName(name));
    }

    private static X509Certificate2 SelfSigned(
        RSA rsa,
        string cn,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool ca,
        params string[] dns)
    {
        var request = new CertificateRequest("CN=" + cn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(ca, false, 0, true));
        X509KeyUsageFlags usage = ca
            ? X509KeyUsageFlags.KeyCertSign
            : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, true));
        if (dns.Length > 0)
        {
            var names = new SubjectAlternativeNameBuilder();
            foreach (string name in dns)
            {
                names.AddDnsName(name);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 Issued(RSA rsa, X509Certificate2 issuer, string cn, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var request = new CertificateRequest("CN=" + cn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        return request.Create(issuer, notBefore, notAfter, [1, 2, 3]);
    }

    private static string Fingerprint(byte[] raw)
    {
        string hex = Convert.ToHexString(SHA256.HashData(raw));
        var grouped = new StringBuilder(hex.Length + hex.Length / 2);
        for (int i = 0; i < hex.Length; i += 2)
        {
            if (i > 0)
            {
                grouped.Append(':');
            }

            grouped.Append(hex.AsSpan(i, 2));
        }

        return grouped.ToString();
    }

    private static string FirstBodyLine(string pem)
    {
        string[] lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.First(line => !line.StartsWith("-----", StringComparison.Ordinal));
    }
}
