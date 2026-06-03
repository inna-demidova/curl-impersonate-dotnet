namespace CurlImpersonate.Bindings;

public sealed class ClientOptions
{
    public ImpersonateTarget? DefaultTarget { get; set; } = ImpersonateTarget.Chrome116;
    public bool FollowRedirects { get; set; } = true;
    public int MaxRedirects { get; set; } = 10;
    public int TimeoutMs { get; set; } = 30_000;
    public int ConnectTimeoutMs { get; set; } = 10_000;
    public string? AcceptEncoding { get; set; } = "gzip, deflate, br";
    public bool VerifyPeer { get; set; } = true;
    public bool VerifyHost { get; set; } = true;
    public bool CookieJarEnabled { get; set; } = true;
    public ProxyConfig? Proxy { get; set; }
    public bool Verbose { get; set; }
    public Dictionary<string, string> DefaultHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
}
