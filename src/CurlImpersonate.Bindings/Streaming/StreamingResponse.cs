using System.Net;

namespace CurlImpersonate.Bindings;

/// <summary>
/// Result of <see cref="ImpersonateClient.SendStreamingAsync"/>. Despite the name, the body is
/// <b>fully buffered in memory</b> before the response is returned — <see cref="Body"/> is a
/// read-only view over that buffer, not an incrementally-streamed network read.
/// </summary>
public sealed class StreamingResponse : IAsyncDisposable
{
    private readonly Stream _body;

    public HttpStatusCode StatusCode { get; }
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;

    /// <summary>Read-only view over the fully-buffered response body.</summary>
    public Stream Body => _body;

    /// <summary>Number of native write-callback invocations during the transfer (chunk count).</summary>
    public int ChunkCount { get; }

    internal StreamingResponse(HttpStatusCode statusCode, Stream body, int chunkCount)
    {
        StatusCode = statusCode;
        _body = body;
        ChunkCount = chunkCount;
    }

    public ValueTask DisposeAsync() => _body.DisposeAsync();
}
