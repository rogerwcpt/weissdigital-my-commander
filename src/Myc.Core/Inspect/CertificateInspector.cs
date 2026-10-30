using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Myc.Core.Inspect;

public enum CertificateStatus
{
    Ready,
    NeedsPassword,
    Refused,
}

/// <summary>A certificate page, a request for a password, or a reason to stop.</summary>
public sealed record CertificateInspection(CertificateStatus Status, string Text, string? Error)
{
    public static CertificateInspection Ready(string text) => new(CertificateStatus.Ready, text, null);

    public static CertificateInspection AskPassword(string? error) => new(CertificateStatus.NeedsPassword, "", error);

    public static CertificateInspection Refuse(string error) => new(CertificateStatus.Refused, "", error);
}

/// <summary>
/// Reads a certificate file into a short page. A private key is never printed, and a PKCS#12
/// bag is loaded only into memory.
/// </summary>
public static class CertificateInspector
{
    public const int MaxBytes = 1024 * 1024;

    private const string PrivateKeyPage = "Private key. Not shown.";

    private static readonly string[] CertificateExtensions =
    [
        ".pem", ".crt", ".cer", ".der", ".p7b", ".p7c", ".pfx", ".p12", ".csr", ".key",
    ];

    // 1.2.840.113549.1.12, the PKCS#12 arc. A lone certificate does not carry it.
    private static readonly byte[] Pkcs12Oid = [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x0C];

    public static bool LooksLikeName(string name)
    {
        string ext = Path.GetExtension(name);
        foreach (string candidate in CertificateExtensions)
        {
            if (ext.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the leading bytes are PEM armor. A NUL means the file is binary.</summary>
    public static bool LooksLikePem(ReadOnlySpan<byte> data)
    {
        int count = Math.Min(data.Length, 8192);
        for (int i = 0; i < count; i++)
        {
            if (data[i] == 0)
            {
                return false;
            }
        }

        string head = Encoding.ASCII.GetString(data[..count]);
        return head.Contains("-----BEGIN ", StringComparison.Ordinal);
    }

    public static CertificateInspection Read(string path, string? password, DateTimeOffset now)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return CertificateInspection.Refuse("No such file.");
            }

            if ((info.Attributes & FileAttributes.Directory) != 0)
            {
                return CertificateInspection.Refuse("Not a file.");
            }

            if (info.Length > MaxBytes)
            {
                return CertificateInspection.Refuse("This file is larger than 1 MB.");
            }

            bool pkcs12 = info.Extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                || info.Extension.Equals(".p12", StringComparison.OrdinalIgnoreCase);
            return Inspect(File.ReadAllBytes(path), password, now, pkcs12);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CertificateInspection.Refuse(exception.Message);
        }
    }

    public static CertificateInspection Inspect(ReadOnlySpan<byte> data, string? password, DateTimeOffset now, bool pkcs12 = false)
    {
        if (data.Length > MaxBytes)
        {
            return CertificateInspection.Refuse("This file is larger than 1 MB.");
        }

        if (data.Length == 0)
        {
            return CertificateInspection.Refuse("This is not a certificate.");
        }

        if (LooksLikePem(data))
        {
            return InspectPem(NormalizePem(Encoding.UTF8.GetString(data)), now);
        }

        return InspectBinary(data.ToArray(), password, now, pkcs12 || IsPkcs12(data));
    }

    private static CertificateInspection InspectPem(string text, DateTimeOffset now)
    {
        List<PemBlock> blocks = ReadBlocks(text);
        if (blocks.Count == 0)
        {
            return CertificateInspection.Refuse("This is not a certificate.");
        }

        bool privateKey = blocks.Exists(block => IsPrivate(block.Label));
        bool anyCertificate = blocks.Exists(block => IsCertificate(block.Label));
        bool anyPkcs7 = blocks.Exists(block => IsPkcs7(block.Label));
        bool anyRequest = blocks.Exists(block => IsRequest(block.Label));
        bool anyPublic = blocks.Exists(block => IsPublic(block.Label));

        if (privateKey && !anyCertificate && !anyPkcs7 && !anyRequest && !anyPublic)
        {
            return CertificateInspection.Ready(PrivateKeyPage);
        }

        var facts = new List<CertFacts>();
        if (anyCertificate)
        {
            foreach (PemBlock block in blocks)
            {
                if (!IsCertificate(block.Label))
                {
                    continue;
                }

                try
                {
                    using X509Certificate2 cert = X509CertificateLoader.LoadCertificate(block.Der);
                    facts.Add(Summarize(cert));
                }
                catch (CryptographicException)
                {
                    return CertificateInspection.Refuse("This is not a certificate.");
                }
            }
        }

        if (anyPkcs7)
        {
            foreach (PemBlock block in blocks)
            {
                if (!IsPkcs7(block.Label))
                {
                    continue;
                }

                List<CertFacts>? part = TryPkcs7(block.Der);
                if (part is null)
                {
                    return CertificateInspection.Refuse("This is not a certificate.");
                }

                facts.AddRange(part);
            }
        }

        if (facts.Count > 0)
        {
            return CertificateInspection.Ready(Format(facts, privateKey, pkcs12: false, now));
        }

        if (anyRequest)
        {
            PemBlock request = blocks.First(block => IsRequest(block.Label));
            return DescribeRequest(request.Text);
        }

        if (anyPublic)
        {
            PemBlock key = blocks.First(block => IsPublic(block.Label));
            return DescribePublicKey(key.Text);
        }

        return CertificateInspection.Refuse("This is not a certificate.");
    }

    private static CertificateInspection InspectBinary(byte[] data, string? password, DateTimeOffset now, bool pkcs12)
    {
        if (!pkcs12)
        {
            try
            {
                using X509Certificate2 single = X509CertificateLoader.LoadCertificate(data);
                return CertificateInspection.Ready(Format([Summarize(single)], privateKey: false, pkcs12: false, now));
            }
            catch (CryptographicException)
            {
                // A PKCS#7 bag or a PKCS#12 bag is not a single certificate.
            }

            List<CertFacts>? bag = TryPkcs7(data);
            if (bag is { Count: > 0 })
            {
                return CertificateInspection.Ready(Format(bag, privateKey: false, pkcs12: false, now));
            }
        }

        if (pkcs12 || IsPkcs12(data))
        {
            return OpenPkcs12(data, password, now);
        }

        return CertificateInspection.Refuse("This is not a certificate.");
    }

    private static CertificateInspection OpenPkcs12(byte[] data, string? password, DateTimeOffset now)
    {
        if (TryPkcs12(data, password, now) is { } opened)
        {
            return opened;
        }

        return password is null
            ? CertificateInspection.AskPassword(null)
            : CertificateInspection.AskPassword("Wrong password.");
    }

    /// <summary>
    /// Ephemeral keys stay out of a keychain. macOS rejects that flag, so the certificate
    /// is loaded with the default and disposed, which drops the key.
    /// </summary>
    private static X509KeyStorageFlags Pkcs12Flags() =>
        OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;

    private static CertificateInspection? TryPkcs12(byte[] data, string? password, DateTimeOffset now)
    {
        X509Certificate2Collection loaded;
        try
        {
            loaded = X509CertificateLoader.LoadPkcs12Collection(data, password, Pkcs12Flags());
        }
        catch (CryptographicException)
        {
            return null;
        }

        try
        {
            if (loaded.Count == 0)
            {
                return null;
            }

            bool privateKey = false;
            var facts = new List<CertFacts>();
            foreach (X509Certificate2 cert in loaded)
            {
                if (cert.HasPrivateKey)
                {
                    privateKey = true;
                }

                facts.Add(Summarize(cert));
            }

            return CertificateInspection.Ready(Format(facts, privateKey, pkcs12: true, now));
        }
        finally
        {
            DisposeAll(loaded);
        }
    }

    private static List<CertFacts>? TryPkcs7(byte[] data)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(data);
            if (cms.Certificates.Count == 0)
            {
                return null;
            }

            var facts = new List<CertFacts>(cms.Certificates.Count);
            foreach (X509Certificate2 cert in cms.Certificates)
            {
                facts.Add(Summarize(cert));
            }

            DisposeAll(cms.Certificates);
            return facts;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static CertificateInspection DescribeRequest(string pem)
    {
        CertificateRequest request;
        try
        {
            request = CertificateRequest.LoadSigningRequestPem(
                pem,
                HashAlgorithmName.SHA256,
                CertificateRequestLoadOptions.SkipSignatureValidation | CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions,
                RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return CertificateInspection.Refuse("This is not a certificate.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("Certificate request");
        builder.AppendLine();
        Field(builder, "Subject", request.SubjectName.Name);
        Field(builder, "Public key", DescribeKey(request.PublicKey));
        AppendNames(builder, NamesOf(request.CertificateExtensions));
        return CertificateInspection.Ready(builder.ToString().TrimEnd());
    }

    private static CertificateInspection DescribePublicKey(string pem)
    {
        if (!TryKey(pem, out string described))
        {
            return CertificateInspection.Refuse("This is not a certificate.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("Public key");
        builder.AppendLine();
        Field(builder, "Algorithm", described);
        return CertificateInspection.Ready(builder.ToString().TrimEnd());
    }

    private static bool TryKey(string pem, out string described)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            described = "RSA " + rsa.KeySize;
            return true;
        }
        catch (CryptographicException)
        {
            // Not an RSA key.
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            described = "ECDSA " + ecdsa.KeySize;
            return true;
        }
        catch (CryptographicException)
        {
            described = "";
            return false;
        }
    }

    private static string Format(IReadOnlyList<CertFacts> certs, bool privateKey, bool pkcs12, DateTimeOffset now)
    {
        IReadOnlyList<CertFacts> ordered = LeafFirst(certs);
        var builder = new StringBuilder();
        if (pkcs12)
        {
            builder.AppendLine("PKCS#12");
            builder.AppendLine();
            Field(builder, "Private key", privateKey ? "Yes" : "No");
            builder.AppendLine();
        }
        else if (privateKey)
        {
            Field(builder, "Private key", "Yes");
            builder.AppendLine();
        }

        if (ordered.Count > 1)
        {
            builder.AppendLine(ordered.Count + " certificates");
            builder.AppendLine();
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            if (ordered.Count > 1)
            {
                builder.AppendLine((i + 1).ToString());
            }

            AppendOne(builder, ordered[i], now);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendOne(StringBuilder builder, CertFacts cert, DateTimeOffset now)
    {
        Field(builder, "Subject", cert.Subject);
        Field(builder, "Issuer", cert.Issuer);
        Field(builder, "Serial", cert.Serial);
        Field(builder, "Valid from", Clock(cert.NotBefore));
        Field(builder, "Valid to", Clock(cert.NotAfter));
        Field(builder, "Status", Status(cert.NotBefore, cert.NotAfter, now));
        Field(builder, "Signature", cert.Signature);
        Field(builder, "Public key", cert.PublicKey);
        Field(builder, "SHA-256", cert.Fingerprint);
        AppendNames(builder, cert.Names);
        Field(builder, "CA", cert.CertificateAuthority ? "Yes" : "No");
        if (cert.KeyUsage is not null)
        {
            Field(builder, "Key usage", cert.KeyUsage);
        }
    }

    private static void AppendNames(StringBuilder builder, AltNames names)
    {
        if (names.Dns.Count > 0)
        {
            Field(builder, "DNS names", Join(names.Dns));
        }

        if (names.Addresses.Count > 0)
        {
            Field(builder, "Addresses", Join(names.Addresses));
        }
    }

    private static string Join(IReadOnlyList<string> names)
    {
        const int Limit = 12;
        if (names.Count <= Limit)
        {
            return string.Join(", ", names);
        }

        return string.Join(", ", names.Take(Limit)) + ", and " + (names.Count - Limit) + " more";
    }

    private static string Status(DateTimeOffset notBefore, DateTimeOffset notAfter, DateTimeOffset now)
    {
        if (now < notBefore)
        {
            return "Not valid yet";
        }

        if (now >= notAfter)
        {
            return "Expired";
        }

        int days = (int)(notAfter - now).TotalDays;
        return days < 1 ? "Valid for less than a day" : "Valid for " + days + " days";
    }

    private static string Clock(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

    private static void Field(StringBuilder builder, string label, string value)
    {
        builder.Append(label.PadRight(12));
        builder.AppendLine(value);
    }

    private static CertFacts Summarize(X509Certificate2 cert)
    {
        AltNames names = NamesOf(cert.Extensions);
        string? usage = null;
        bool ca = false;
        foreach (X509Extension extension in cert.Extensions)
        {
            if (extension is X509BasicConstraintsExtension basic)
            {
                ca = basic.CertificateAuthority;
            }

            if (extension is X509KeyUsageExtension keyUsage)
            {
                usage = DescribeUsage(keyUsage.KeyUsages);
            }
        }

        return new CertFacts(
            cert.Subject,
            cert.Issuer,
            cert.SerialNumber,
            ToUtc(cert.NotBefore),
            ToUtc(cert.NotAfter),
            cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "",
            DescribeKey(cert.PublicKey),
            Fingerprint(cert.RawData),
            names,
            ca,
            usage);
    }

    private static AltNames NamesOf(IEnumerable<X509Extension> extensions)
    {
        var dns = new List<string>();
        var addresses = new List<string>();
        foreach (X509Extension extension in extensions)
        {
            if (extension is not X509SubjectAlternativeNameExtension san)
            {
                continue;
            }

            dns.AddRange(san.EnumerateDnsNames());
            foreach (IPAddress address in san.EnumerateIPAddresses())
            {
                addresses.Add(address.ToString());
            }
        }

        return new AltNames(dns, addresses);
    }

    private static void DisposeAll(X509Certificate2Collection certs)
    {
        foreach (X509Certificate2 cert in certs)
        {
            cert.Dispose();
        }
    }

    private static string DescribeKey(PublicKey key)
    {
        using RSA? rsa = key.GetRSAPublicKey();
        if (rsa is not null)
        {
            return "RSA " + rsa.KeySize;
        }

        using ECDsa? ecdsa = key.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            return "ECDSA " + ecdsa.KeySize;
        }

        return key.Oid?.FriendlyName ?? key.Oid?.Value ?? "Public key";
    }

    private static string DescribeUsage(X509KeyUsageFlags flags)
    {
        var names = new List<string>();
        void Add(X509KeyUsageFlags flag, string name)
        {
            if ((flags & flag) != 0)
            {
                names.Add(name);
            }
        }

        Add(X509KeyUsageFlags.DigitalSignature, "Digital signature");
        Add(X509KeyUsageFlags.NonRepudiation, "Non-repudiation");
        Add(X509KeyUsageFlags.KeyEncipherment, "Key encipherment");
        Add(X509KeyUsageFlags.DataEncipherment, "Data encipherment");
        Add(X509KeyUsageFlags.KeyAgreement, "Key agreement");
        Add(X509KeyUsageFlags.KeyCertSign, "Certificate signing");
        Add(X509KeyUsageFlags.CrlSign, "CRL signing");
        Add(X509KeyUsageFlags.EncipherOnly, "Encipher only");
        Add(X509KeyUsageFlags.DecipherOnly, "Decipher only");
        return names.Count == 0 ? flags.ToString() : string.Join(", ", names);
    }

    private static string Fingerprint(byte[] raw)
    {
        byte[] hash = SHA256.HashData(raw);
        var text = new StringBuilder(hash.Length * 3);
        for (int i = 0; i < hash.Length; i++)
        {
            if (i > 0)
            {
                text.Append(':');
            }

            text.Append(hash[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static DateTimeOffset ToUtc(DateTime value)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime(),
        };
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private static IReadOnlyList<CertFacts> LeafFirst(IReadOnlyList<CertFacts> certs)
    {
        if (certs.Count < 2)
        {
            return certs;
        }

        var issuerOfOther = new HashSet<int>();
        for (int i = 0; i < certs.Count; i++)
        {
            for (int j = 0; j < certs.Count; j++)
            {
                if (i != j && certs[j].Issuer == certs[i].Subject)
                {
                    issuerOfOther.Add(i);
                }
            }
        }

        int leaf = -1;
        for (int i = 0; i < certs.Count; i++)
        {
            if (issuerOfOther.Contains(i))
            {
                continue;
            }

            if (leaf >= 0)
            {
                return certs;
            }

            leaf = i;
        }

        if (leaf < 0)
        {
            return certs;
        }

        var ordered = new List<CertFacts>();
        var seen = new HashSet<int>();
        int current = leaf;
        while (current >= 0 && seen.Count < certs.Count)
        {
            ordered.Add(certs[current]);
            seen.Add(current);
            int next = -1;
            for (int i = 0; i < certs.Count; i++)
            {
                if (!seen.Contains(i) && certs[i].Subject == certs[current].Issuer)
                {
                    next = i;
                    break;
                }
            }

            current = next;
        }

        for (int i = 0; i < certs.Count; i++)
        {
            if (!seen.Contains(i))
            {
                ordered.Add(certs[i]);
            }
        }

        return ordered;
    }

    private static List<PemBlock> ReadBlocks(string text)
    {
        var blocks = new List<PemBlock>();
        int offset = 0;
        while (offset < text.Length && PemEncoding.TryFind(text.AsSpan(offset), out PemFields fields))
        {
            ReadOnlySpan<char> window = text.AsSpan(offset);
            string label = window[fields.Label].ToString();
            string pem = window[fields.Location].ToString();
            byte[] der = new byte[fields.DecodedDataLength];
            if (!Convert.TryFromBase64Chars(window[fields.Base64Data], der, out int written) || written != der.Length)
            {
                break;
            }

            blocks.Add(new PemBlock(label, pem, der));
            int end = fields.Location.End.GetOffset(window.Length);
            if (end <= 0)
            {
                break;
            }

            offset += end;
        }

        return blocks;
    }

    /// <summary>A bundle written without a newline between blocks still parses.</summary>
    private static string NormalizePem(string text) =>
        text.Replace("-----BEGIN ", "\n-----BEGIN ", StringComparison.Ordinal).TrimStart();

    private static bool IsPkcs12(ReadOnlySpan<byte> data) => data.IndexOf(Pkcs12Oid) >= 0;

    private static bool IsPrivate(string label) =>
        label is "PRIVATE KEY" or "RSA PRIVATE KEY" or "EC PRIVATE KEY" or "ENCRYPTED PRIVATE KEY" or "OPENSSH PRIVATE KEY";

    private static bool IsCertificate(string label) =>
        label is "CERTIFICATE" or "X509 CERTIFICATE" or "TRUSTED CERTIFICATE";

    private static bool IsPkcs7(string label) => label is "PKCS7" or "PKCS #7 SIGNED DATA";

    private static bool IsRequest(string label) => label is "CERTIFICATE REQUEST" or "NEW CERTIFICATE REQUEST";

    private static bool IsPublic(string label) => label is "PUBLIC KEY" or "RSA PUBLIC KEY";

    private sealed record CertFacts(
        string Subject,
        string Issuer,
        string Serial,
        DateTimeOffset NotBefore,
        DateTimeOffset NotAfter,
        string Signature,
        string PublicKey,
        string Fingerprint,
        AltNames Names,
        bool CertificateAuthority,
        string? KeyUsage);

    private readonly record struct AltNames(IReadOnlyList<string> Dns, IReadOnlyList<string> Addresses);

    private readonly record struct PemBlock(string Label, string Text, byte[] Der);
}
