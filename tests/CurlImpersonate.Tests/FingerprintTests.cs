using System.Net;
using CurlImpersonate.Bindings;
using Newtonsoft.Json.Linq;

namespace CurlImpersonate.Tests;

/// <summary>
/// The key impersonation proof. Rather than pinning a brittle exact JA3 hash (Chrome randomizes
/// TLS extension order per connection, and hashes change with every upstream version), this asserts
/// the properties that actually prove impersonation is working:
///   1. curl_easy_impersonate accepts the target (the request succeeds),
///   2. an impersonated TLS ClientHello differs from non-impersonated curl, and
///   3. different browser targets produce different fingerprints.
/// Uses https://tls.peet.ws/api/all, which reflects the JA3/JA4 of the request it received.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Fingerprint")]
[Collection("native")]
public class FingerprintTests : IDisposable
{
    private readonly ImpersonateClient _chrome;
    private readonly ImpersonateClient _firefox;
    private readonly ImpersonateClient _bareCurl;

    public FingerprintTests()
    {
        _chrome   = new ImpersonateClient(new ClientOptions { DefaultTarget = ImpersonateTarget.Chrome131,  TimeoutMs = 20_000 });
        _firefox  = new ImpersonateClient(new ClientOptions { DefaultTarget = ImpersonateTarget.Firefox133, TimeoutMs = 20_000 });
        // No impersonation → curl-impersonate's libcurl presents its own (non-browser) ClientHello.
        _bareCurl = new ImpersonateClient(new ClientOptions { DefaultTarget = null, TimeoutMs = 20_000 });
    }

    public void Dispose()
    {
        _chrome.Dispose();
        _firefox.Dispose();
        _bareCurl.Dispose();
    }

    private readonly record struct Fingerprint(string Ja3, string Ja3Hash, string Ja4);

    private static async Task<Fingerprint> GetFingerprintAsync(ImpersonateClient client)
    {
        var resp = await client.GetAsync("https://tls.peet.ws/api/all");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var tls = JObject.Parse(resp.GetBodyAsString())["tls"];
        Assert.NotNull(tls);
        return new Fingerprint(
            tls!["ja3"]?.ToString()      ?? "",
            tls["ja3_hash"]?.ToString()  ?? "",
            tls["ja4"]?.ToString()       ?? "");
    }

    [Fact]
    public async Task Impersonation_AltersTlsFingerprint_VsNonImpersonatedCurl()
    {
        var chrome = await GetFingerprintAsync(_chrome);
        var bare   = await GetFingerprintAsync(_bareCurl);

        Assert.False(string.IsNullOrEmpty(chrome.Ja3Hash), "chrome ja3_hash missing");
        Assert.False(string.IsNullOrEmpty(bare.Ja3Hash),   "bare ja3_hash missing");

        // If impersonation is wired up, the Chrome ClientHello must NOT look like plain curl's.
        Assert.NotEqual(bare.Ja3Hash, chrome.Ja3Hash);

        // Chrome negotiates TLS 1.3 + HTTP/2 → JA4 begins with "t13" and ends with "h2".
        Assert.StartsWith("t13", chrome.Ja4);
    }

    [Fact]
    public async Task Chrome_And_Firefox_ProduceDifferentFingerprints()
    {
        var chrome  = await GetFingerprintAsync(_chrome);
        var firefox = await GetFingerprintAsync(_firefox);

        Assert.False(string.IsNullOrEmpty(chrome.Ja3Hash),  "chrome ja3_hash missing");
        Assert.False(string.IsNullOrEmpty(firefox.Ja3Hash), "firefox ja3_hash missing");

        // Distinct browser engines must yield distinct TLS fingerprints.
        Assert.NotEqual(chrome.Ja3Hash, firefox.Ja3Hash);
        Assert.NotEqual(chrome.Ja4,     firefox.Ja4);
    }
}
