using System.Net.Http;

namespace CurlImpersonate.Bindings;

public sealed class ImpersonateRequest
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public Uri Uri { get; set; } = null!;
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[]? Body { get; set; }
    public ImpersonateTarget? Target { get; set; }
    public bool DefaultHeaders { get; set; } = true;
    public ProxyConfig? Proxy { get; set; }
    public bool? FollowRedirects { get; set; }
    public int? MaxRedirects { get; set; }
    public int? TimeoutMs { get; set; }
    public int? ConnectTimeoutMs { get; set; }
    public string? AcceptEncoding { get; set; }
    public bool? VerifyPeer { get; set; }
    public bool? VerifyHost { get; set; }
    public string? Cookie { get; set; }
    public bool? CookieJarEnabled { get; set; }
    public bool Verbose { get; set; }

    public ImpersonateRequest(Uri uri) => Uri = uri;
    public ImpersonateRequest(string url) => Uri = new Uri(url);
}
