using System.Net;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Verifies that the CURLSH cookie jar persists cookies across requests in the same session.
/// </summary>
[Trait("Category", "Integration")]
[Collection("native")]
public class CookieAndSessionReuseTests : IDisposable
{
    private readonly ImpersonateClient _client;

    public CookieAndSessionReuseTests()
    {
        _client = new ImpersonateClient(new ClientOptions
        {
            DefaultTarget = ImpersonateTarget.Chrome131,
            CookieJarEnabled = true,
            TimeoutMs = 15_000,
        });
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task CookieSetThenRead_PersistsAcrossRequests()
    {
        // httpbin sets a cookie named "testcookie" with value "abc"
        await _client.GetAsync("https://httpbin.org/cookies/set/testcookie/abc");

        // Second request to /cookies should echo back any cookies we're carrying
        var resp = await _client.GetAsync("https://httpbin.org/cookies");
        var body = resp.GetBodyAsString();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("testcookie", body);
        Assert.Contains("abc", body);
    }
}
