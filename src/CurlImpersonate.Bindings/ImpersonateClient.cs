using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Bindings;

/// <summary>
/// Reusable HTTP client that impersonates real browser TLS/HTTP2 fingerprints.
/// Wraps a CURLSH session for cookie-jar + connection + DNS reuse across requests.
/// Thread-safe for concurrent SendAsync calls.
/// </summary>
public sealed class ImpersonateClient : IDisposable, IAsyncDisposable
{
    private IntPtr _session;
    private bool _disposed;
    private readonly ClientOptions _options;

    public ImpersonateClient(ClientOptions? options = null)
    {
        _options = options ?? new ClientOptions();
        NativeLoader.EnsureInitialized();
        _session = NativeMethods.SessionCreate();
        if (_session == IntPtr.Zero)
        {
            var err = Marshal.PtrToStringAnsi(NativeMethods.LastError()) ?? "unknown";
            throw new CurlImpersonateException($"cidn_session_create failed: {err}");
        }
        NativeLoader.RegisterClient();
    }

    // -----------------------------------------------------------------
    // Public high-level API
    // -----------------------------------------------------------------

    public Task<ImpersonateResponse> GetAsync(string url, CancellationToken ct = default) =>
        SendAsync(new ImpersonateRequest(url) { Method = HttpMethod.Get }, ct);

    public Task<ImpersonateResponse> GetAsync(Uri uri, CancellationToken ct = default) =>
        SendAsync(new ImpersonateRequest(uri) { Method = HttpMethod.Get }, ct);

    public Task<ImpersonateResponse> PostAsync(string url, byte[] body, string? contentType = null,
        CancellationToken ct = default)
    {
        var req = new ImpersonateRequest(url) { Method = HttpMethod.Post, Body = body };
        if (contentType != null) req.Headers["Content-Type"] = contentType;
        return SendAsync(req, ct);
    }

    public Task<ImpersonateResponse> PostAsync(string url, string body, string? contentType = "application/json",
        CancellationToken ct = default) =>
        PostAsync(url, Encoding.UTF8.GetBytes(body), contentType, ct);

    public async Task<ImpersonateResponse> SendAsync(ImpersonateRequest request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await Task.Run(() => ExecuteBuffered(request, ct), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the request via the chunked write-callback path. The body is collected from the
    /// native write callbacks and <b>fully buffered in memory</b> before this method returns; the
    /// <see cref="StreamingResponse.Body"/> stream is a read-only view over that buffer (it does
    /// <i>not</i> stream incrementally and provides no backpressure). Use
    /// <see cref="StreamingResponse.ChunkCount"/> to inspect how many write callbacks fired.
    /// The buffered body is capped (~2 GiB) by the native layer; larger responses are aborted.
    /// </summary>
    public async Task<StreamingResponse> SendStreamingAsync(
        ImpersonateRequest request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await Task.Run(() => ExecuteStreaming(request, ct), ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------
    // Internals — buffered path
    // -----------------------------------------------------------------

    private ImpersonateResponse ExecuteBuffered(ImpersonateRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var reqHandle = BuildNativeRequest(request);
        var abortFlag = new int[1];
        var flagPin = GCHandle.Alloc(abortFlag, GCHandleType.Pinned);
        try
        {
            NativeMethods.RequestSetAbortFlag(reqHandle, flagPin.AddrOfPinnedObject());
            using var reg = ct.Register(() => Volatile.Write(ref abortFlag[0], 1));

            int rc = NativeMethods.SessionExecute(_session, reqHandle, out NativeMethods.CidnResponse resp);
            try
            {
                if (rc != 0)
                {
                    if (rc == NativeMethods.CurleAbortedByCallback)
                        ct.ThrowIfCancellationRequested();

                    var errMsg = resp.Error != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(resp.Error) ?? "curl error"
                        : (Marshal.PtrToStringAnsi(NativeMethods.LastError()) is { Length: > 0 } le
                            ? le
                            : Marshal.PtrToStringAnsi(NativeMethods.CurlStrerror(rc)) ?? "unknown");
                    throw new CurlImpersonateException(rc, errMsg);
                }

                var statusCode = (HttpStatusCode)resp.StatusCode;

                var headersBlob = resp.HeadersBlob != IntPtr.Zero
                    ? Marshal.PtrToStringUTF8(resp.HeadersBlob) ?? string.Empty
                    : string.Empty;
                var headers = ImpersonateResponse.ParseHeaders(headersBlob);

                var bodyLen = (int)resp.BodyLen;
                var body = new byte[bodyLen];
                if (bodyLen > 0 && resp.Body != IntPtr.Zero)
                    Marshal.Copy(resp.Body, body, 0, bodyLen);

                Uri? effectiveUrl = null;
                if (resp.EffectiveUrl != IntPtr.Zero)
                {
                    var effStr = Marshal.PtrToStringUTF8(resp.EffectiveUrl);
                    if (effStr != null) Uri.TryCreate(effStr, UriKind.Absolute, out effectiveUrl);
                }

                return new ImpersonateResponse(statusCode, headers, body, effectiveUrl);
            }
            finally
            {
                NativeMethods.ResponseFree(ref resp);
            }
        }
        finally
        {
            flagPin.Free();
            NativeMethods.RequestDestroy(reqHandle);
        }
    }

    // -----------------------------------------------------------------
    // Internals — streaming path
    // -----------------------------------------------------------------

    private sealed class StreamState
    {
        public readonly List<byte[]> Chunks = [];
        public int ChunkCount;
    }

    // Static unmanaged callbacks — use delegate* unmanaged[Cdecl] (R3).
    // State is passed through userdata as a GCHandle token; no delegate GC lifetime issue.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe UIntPtr StreamBodyCallback(byte* data, UIntPtr len, void* userdata)
    {
        var count = (int)(ulong)len;
        var state = (StreamState)GCHandle.FromIntPtr((IntPtr)userdata).Target!;
        var chunk = new byte[count];
        new ReadOnlySpan<byte>(data, count).CopyTo(chunk);
        lock (state.Chunks) state.Chunks.Add(chunk);
        Interlocked.Increment(ref state.ChunkCount);
        return len;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe UIntPtr StreamHeaderCallback(byte* data, UIntPtr len, void* userdata)
    {
        // Headers ignored in streaming path — status code comes from out_status.
        return len;
    }

    private unsafe StreamingResponse ExecuteStreaming(ImpersonateRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var state = new StreamState();
        var stateHandle = GCHandle.Alloc(state);
        var reqHandle = BuildNativeRequest(request);

        var abortFlag = new int[1];
        var flagPin = GCHandle.Alloc(abortFlag, GCHandleType.Pinned);
        try
        {
            NativeMethods.RequestSetAbortFlag(reqHandle, flagPin.AddrOfPinnedObject());
            using var reg = ct.Register(() => Volatile.Write(ref abortFlag[0], 1));

            var writePtr  = (nint)(delegate* unmanaged[Cdecl]<byte*, UIntPtr, void*, UIntPtr>)&StreamBodyCallback;
            var headerPtr = (nint)(delegate* unmanaged[Cdecl]<byte*, UIntPtr, void*, UIntPtr>)&StreamHeaderCallback;

            int rc = NativeMethods.SessionExecuteStream(
                _session, reqHandle,
                writePtr, GCHandle.ToIntPtr(stateHandle),
                headerPtr, 0,
                out long outStatus);

            if (rc != 0)
            {
                if (rc == NativeMethods.CurleAbortedByCallback)
                    ct.ThrowIfCancellationRequested();

                var errMsg = Marshal.PtrToStringAnsi(NativeMethods.LastError()) is { Length: > 0 } le
                    ? le
                    : Marshal.PtrToStringAnsi(NativeMethods.CurlStrerror(rc)) ?? "unknown";
                throw new CurlImpersonateException(rc, errMsg);
            }

            var totalLen = state.Chunks.Sum(c => c.Length);
            var bodyStream = new MemoryStream(totalLen);
            foreach (var chunk in state.Chunks)
                bodyStream.Write(chunk, 0, chunk.Length);
            bodyStream.Position = 0;

            return new StreamingResponse((HttpStatusCode)outStatus, bodyStream, state.ChunkCount);
        }
        finally
        {
            flagPin.Free();
            stateHandle.Free();
            NativeMethods.RequestDestroy(reqHandle);
        }
    }

    // -----------------------------------------------------------------
    // Internals — native request builder
    // -----------------------------------------------------------------

    private IntPtr BuildNativeRequest(ImpersonateRequest r)
    {
        var h = NativeMethods.RequestCreate();
        if (h == IntPtr.Zero)
            throw new CurlImpersonateException("cidn_request_create returned null");

        NativeMethods.RequestSetUrl(h, r.Uri.AbsoluteUri);
        NativeMethods.RequestSetMethod(h, r.Method.Method);

        var target = r.Target ?? _options.DefaultTarget;
        if (target != null)
            NativeMethods.RequestSetImpersonate(h, target.Value, r.DefaultHeaders ? 1 : 0);

        var allHeaders = new Dictionary<string, string>(_options.DefaultHeaders, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in r.Headers) allHeaders[kv.Key] = kv.Value;
        foreach (var kv in allHeaders)
            NativeMethods.RequestAddHeader(h, $"{kv.Key}: {kv.Value}");

        if (r.Body != null && r.Body.Length > 0)
        {
            var pin = GCHandle.Alloc(r.Body, GCHandleType.Pinned);
            try
            {
                NativeMethods.RequestSetBody(h, pin.AddrOfPinnedObject(), (UIntPtr)r.Body.Length);
            }
            finally
            {
                pin.Free();
            }
        }

        var proxy = r.Proxy ?? _options.Proxy;
        if (proxy != null)
            NativeMethods.RequestSetProxy(h, proxy.Url, proxy.UserPwd);

        var follow = r.FollowRedirects ?? _options.FollowRedirects;
        var maxR = r.MaxRedirects ?? _options.MaxRedirects;
        NativeMethods.RequestSetFollowRedirects(h, follow ? 1 : 0, maxR);

        var total = r.TimeoutMs ?? _options.TimeoutMs;
        var connect = r.ConnectTimeoutMs ?? _options.ConnectTimeoutMs;
        NativeMethods.RequestSetTimeoutMs(h, total, connect);

        var enc = r.AcceptEncoding ?? _options.AcceptEncoding;
        if (enc != null) NativeMethods.RequestSetAcceptEncoding(h, enc);

        var vPeer = r.VerifyPeer ?? _options.VerifyPeer;
        var vHost = r.VerifyHost ?? _options.VerifyHost;
        NativeMethods.RequestSetVerify(h, vPeer ? 1 : 0, vHost ? 1 : 0);

        if (r.Cookie != null) NativeMethods.RequestSetCookie(h, r.Cookie);

        var cookieJar = r.CookieJarEnabled ?? _options.CookieJarEnabled;
        NativeMethods.RequestSetCookieJarEnabled(h, cookieJar ? 1 : 0);

        if (r.Verbose || _options.Verbose)
            NativeMethods.RequestSetVerbose(h, 1);

        return h;
    }

    // -----------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session != IntPtr.Zero)
        {
            NativeMethods.SessionDestroy(_session);
            _session = IntPtr.Zero;
            NativeLoader.UnregisterClient();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
