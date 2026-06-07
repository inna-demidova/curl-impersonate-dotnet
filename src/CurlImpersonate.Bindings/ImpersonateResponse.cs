using System.Net;
using System.Text;

namespace CurlImpersonate.Bindings;

public sealed class ImpersonateResponse
{
    public HttpStatusCode StatusCode { get; }
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;

    /// <summary>
    /// All response headers of the final response (after any redirects), in wire order and
    /// preserving duplicates (e.g. multiple <c>Set-Cookie</c> lines). Use <see cref="GetHeader"/>
    /// or <see cref="GetHeaders"/> for case-insensitive lookups.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }

    public byte[] Body { get; }
    public Uri? EffectiveUrl { get; }

    public ImpersonateResponse(
        HttpStatusCode statusCode,
        IReadOnlyList<KeyValuePair<string, string>> headers,
        byte[] body,
        Uri? effectiveUrl)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        EffectiveUrl = effectiveUrl;
    }

    /// <summary>Returns the first value of the named header (case-insensitive), or null.</summary>
    public string? GetHeader(string name)
    {
        foreach (var kv in Headers)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }

    /// <summary>Returns all values of the named header (case-insensitive), in wire order.</summary>
    public IEnumerable<string> GetHeaders(string name)
    {
        foreach (var kv in Headers)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                yield return kv.Value;
    }

    /// <summary>All <c>Set-Cookie</c> header values from the final response.</summary>
    public IReadOnlyList<string> SetCookies => GetHeaders("Set-Cookie").ToList();

    public string GetBodyAsString(Encoding? encoding = null) =>
        (encoding ?? Encoding.UTF8).GetString(Body);

    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
            throw new CurlImpersonateException($"HTTP request failed with status {(int)StatusCode} {StatusCode}.");
    }

    /// <summary>
    /// Parses the concatenated header blob. The blob may contain several header blocks (one per
    /// redirect hop), separated by a blank line; only the headers of the <b>final</b> response are
    /// returned. Duplicate headers and wire order are preserved.
    /// </summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> ParseHeaders(string headersBlob)
    {
        // curl delivers each header block terminated by a blank line, and the whole blob ends with
        // a trailing blank line. Accumulate per-block and keep the last non-empty block so we
        // return only the final response's headers (after redirects).
        var current = new List<KeyValuePair<string, string>>();
        List<KeyValuePair<string, string>>? lastBlock = null;

        foreach (var rawLine in headersBlob.Split('\n'))
        {
            var line = rawLine.Trim('\r', '\n');
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    lastBlock = current;
                    current = new List<KeyValuePair<string, string>>();
                }
                continue;
            }

            // Status lines ("HTTP/1.1 200 OK") have no colon-separated name → skip.
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            current.Add(new KeyValuePair<string, string>(name, value));
        }

        if (current.Count > 0) lastBlock = current;
        return lastBlock ?? [];
    }
}
