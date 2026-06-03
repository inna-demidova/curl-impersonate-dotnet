namespace CurlImpersonate.Bindings.Cookies;

/// <summary>
/// Fluent builder for a cookie string passed to <see cref="ImpersonateRequest.Cookie"/>.
/// </summary>
public sealed class CookieOptions
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.OrdinalIgnoreCase);

    public CookieOptions Set(string name, string value)
    {
        _cookies[name] = value;
        return this;
    }

    public CookieOptions Remove(string name)
    {
        _cookies.Remove(name);
        return this;
    }

    public bool IsEmpty => _cookies.Count == 0;

    /// <summary>Builds the Cookie header value, e.g. <c>name1=val1; name2=val2</c>.</summary>
    public string Build() =>
        string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}"));

    public override string ToString() => Build();
}
