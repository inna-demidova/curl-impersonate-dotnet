using System.Net;
using System.Text;

namespace CurlImpersonate.Bindings;

public sealed class ImpersonateResponse
{
    public HttpStatusCode StatusCode { get; }
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
    public IReadOnlyDictionary<string, string> Headers { get; }
    public byte[] Body { get; }
    public Uri? EffectiveUrl { get; }

    public ImpersonateResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, string> headers,
        byte[] body,
        Uri? effectiveUrl)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        EffectiveUrl = effectiveUrl;
    }

    public string GetBodyAsString(Encoding? encoding = null) =>
        (encoding ?? Encoding.UTF8).GetString(Body);

    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
            throw new CurlImpersonateException($"HTTP request failed with status {(int)StatusCode} {StatusCode}.");
    }

    internal static IReadOnlyDictionary<string, string> ParseHeaders(string headersBlob)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in headersBlob.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim('\r', '\n');
            var colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;
            var name = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            dict[name] = value;
        }
        return dict;
    }
}
