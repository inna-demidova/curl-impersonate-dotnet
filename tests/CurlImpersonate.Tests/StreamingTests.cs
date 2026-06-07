using System.Net;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Verifies the streaming execute path: callback fires multiple times (chunked),
/// all bytes are received, and cancellation aborts mid-transfer.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Streaming")]
[Collection("native")]
public class StreamingTests : IDisposable
{
    private readonly ImpersonateClient _client;

    public StreamingTests()
    {
        _client = new ImpersonateClient(new ClientOptions
        {
            DefaultTarget = ImpersonateTarget.Chrome116,
            TimeoutMs = 30_000,
        });
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task StreamingGet_ReceivesAllBytes()
    {
        var resp = await _client.SendStreamingAsync(
            new ImpersonateRequest("https://httpbin.org/bytes/65536"));

        using var buffer = new MemoryStream();
        await resp.Body.CopyToAsync(buffer);
        await resp.DisposeAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(65536L, buffer.Length);
    }

    [Fact]
    public async Task StreamingGet_CallbackFiredMultipleTimes()
    {
        var resp = await _client.SendStreamingAsync(
            new ImpersonateRequest("https://httpbin.org/bytes/65536"));

        await resp.Body.CopyToAsync(Stream.Null);
        await resp.DisposeAsync();

        Assert.True(resp.ChunkCount > 1,
            $"Expected multiple write callbacks for 65536-byte response, but got {resp.ChunkCount}");
    }

    [Fact]
    public async Task StreamingGet_StatusCode_IsCorrect()
    {
        var resp = await _client.SendStreamingAsync(
            new ImpersonateRequest("https://httpbin.org/status/204"));
        await resp.DisposeAsync();

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task StreamingGet_Cancellation_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.SendStreamingAsync(
                new ImpersonateRequest("https://httpbin.org/delay/10"),
                cts.Token));
    }
}
