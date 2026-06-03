using System.Net;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Basic HTTP smoke tests. Requires network access and the native libs to be staged.
/// </summary>
[Trait("Category", "Integration")]
public class HttpSmokeTests : IDisposable
{
    private readonly ImpersonateClient _client;

    public HttpSmokeTests()
    {
        NativeLoader.Initialize();
        _client = new ImpersonateClient(new ClientOptions
        {
            DefaultTarget = ImpersonateTarget.Chrome131,
            TimeoutMs = 15_000,
            ConnectTimeoutMs = 5_000,
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        NativeLoader.Cleanup();
    }

    [Fact]
    public async Task Get_HttpBin_Returns200()
    {
        var resp = await _client.GetAsync("https://httpbin.org/get");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotEmpty(resp.Body);
    }

    [Fact]
    public async Task Post_WithJsonBody_Returns200()
    {
        var resp = await _client.PostAsync(
            "https://httpbin.org/post",
            """{"hello":"world"}""",
            "application/json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = resp.GetBodyAsString();
        Assert.Contains("hello", body);
    }

    [Fact]
    public async Task Get_WithRedirects_FollowsAndReturns200()
    {
        var resp = await _client.GetAsync("https://httpbin.org/redirect/2");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Get_WithRedirectsDisabled_Returns302()
    {
        var req = new ImpersonateRequest("https://httpbin.org/redirect/1")
        {
            FollowRedirects = false
        };
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
    }

    [Fact]
    public async Task Response_EffectiveUrl_IsSet()
    {
        var resp = await _client.GetAsync("https://httpbin.org/get");
        Assert.NotNull(resp.EffectiveUrl);
    }

    [Fact]
    public async Task EnsureSuccessStatusCode_ThrowsOn404()
    {
        var resp = await _client.GetAsync("https://httpbin.org/status/404");
        Assert.Throws<CurlImpersonateException>(() => resp.EnsureSuccessStatusCode());
    }
}
