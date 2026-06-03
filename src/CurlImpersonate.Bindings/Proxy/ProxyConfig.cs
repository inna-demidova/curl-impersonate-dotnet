namespace CurlImpersonate.Bindings;

public sealed class ProxyConfig
{
    public string Url { get; }
    public string? UserPwd { get; }

    public ProxyConfig(string url, string? userPwd = null)
    {
        Url = url;
        UserPwd = userPwd;
    }
}
