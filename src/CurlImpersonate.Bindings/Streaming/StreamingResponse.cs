using System.Net;

namespace CurlImpersonate.Bindings;

public sealed class StreamingResponse : IAsyncDisposable
{
    private readonly Stream _body;

    public HttpStatusCode StatusCode { get; }
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
    public Stream Body => _body;

    /// <summary>Number of write-callback invocations during the transfer (chunk count).</summary>
    public int ChunkCount { get; }

    internal StreamingResponse(HttpStatusCode statusCode, Stream body, int chunkCount)
    {
        StatusCode = statusCode;
        _body = body;
        ChunkCount = chunkCount;
    }

    public ValueTask DisposeAsync() => _body.DisposeAsync();
}
