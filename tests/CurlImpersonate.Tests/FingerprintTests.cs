using System.Net;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;
using Newtonsoft.Json.Linq;

namespace CurlImpersonate.Tests;

/// <summary>
/// The key impersonation proof: asserts that the TLS fingerprint presented to tls.peet.ws
/// matches known Chrome 131 hashes, and that Firefox produces a *different* JA3.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Fingerprint")]
public class FingerprintTests : IDisposable
{
    // Chrome 131 known fingerprints (from tls.peet.ws — update if upstream changes these).
    private const string Chrome131Ja3 = "8daaf6152771793600d297e69e9f91ea";

    private readonly ImpersonateClient _chromeClient;
    private readonly ImpersonateClient _firefoxClient;

    public FingerprintTests()
    {
        NativeLoader.Initialize();
        _chromeClient  = new ImpersonateClient(new ClientOptions { DefaultTarget = ImpersonateTarget.Chrome131,  TimeoutMs = 20_000 });
        _firefoxClient = new ImpersonateClient(new ClientOptions { DefaultTarget = ImpersonateTarget.Firefox133, TimeoutMs = 20_000 });
    }

    public void Dispose()
    {
        _chromeClient.Dispose();
        _firefoxClient.Dispose();
        NativeLoader.Cleanup();
    }

    [Fact]
    public async Task Chrome131_Ja3Hash_MatchesExpected()
    {
        var resp = await _chromeClient.GetAsync("https://tls.peet.ws/api/all");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = JObject.Parse(resp.GetBodyAsString());
        var ja3 = json["tls"]?["ja3"]?.ToString();
        Assert.False(string.IsNullOrEmpty(ja3), "ja3 not present in response");
        Assert.Equal(Chrome131Ja3, ja3);
    }

    [Fact]
    public async Task Chrome131_And_Firefox_ProduceDifferentJa3()
    {
        var chromeResp  = await _chromeClient.GetAsync("https://tls.peet.ws/api/all");
        var firefoxResp = await _firefoxClient.GetAsync("https://tls.peet.ws/api/all");

        var chromeJa3  = JObject.Parse(chromeResp.GetBodyAsString())["tls"]?["ja3"]?.ToString();
        var firefoxJa3 = JObject.Parse(firefoxResp.GetBodyAsString())["tls"]?["ja3"]?.ToString();

        Assert.False(string.IsNullOrEmpty(chromeJa3),  "chrome ja3 missing");
        Assert.False(string.IsNullOrEmpty(firefoxJa3), "firefox ja3 missing");
        Assert.NotEqual(chromeJa3, firefoxJa3);
    }
}
