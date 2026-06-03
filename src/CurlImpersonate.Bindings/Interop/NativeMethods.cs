using System.Runtime.InteropServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CurlImpersonate.Tests")]

namespace CurlImpersonate.Bindings.Interop;

internal static partial class NativeMethods
{
    private const string LibName = "cidn_wrapper";
    internal const int CurleAbortedByCallback = 42;

    // ── Lifecycle ────────────────────────────────────────────────────────
    [LibraryImport(LibName, EntryPoint = "cidn_global_init", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int GlobalInit(string? libcurlPath);

    [LibraryImport(LibName, EntryPoint = "cidn_global_cleanup")]
    internal static partial void GlobalCleanup();

    [LibraryImport(LibName, EntryPoint = "cidn_last_error")]
    internal static partial IntPtr LastError();

    [LibraryImport(LibName, EntryPoint = "cidn_curl_strerror")]
    internal static partial IntPtr CurlStrerror(int code);

    // ── Session ──────────────────────────────────────────────────────────
    [LibraryImport(LibName, EntryPoint = "cidn_session_create")]
    internal static partial IntPtr SessionCreate();

    [LibraryImport(LibName, EntryPoint = "cidn_session_destroy")]
    internal static partial void SessionDestroy(IntPtr session);

    // ── Request builder ──────────────────────────────────────────────────
    [LibraryImport(LibName, EntryPoint = "cidn_request_create")]
    internal static partial IntPtr RequestCreate();

    [LibraryImport(LibName, EntryPoint = "cidn_request_destroy")]
    internal static partial void RequestDestroy(IntPtr req);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_method", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetMethod(IntPtr req, string method);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_url", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetUrl(IntPtr req, string url);

    [LibraryImport(LibName, EntryPoint = "cidn_request_add_header", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestAddHeader(IntPtr req, string header);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_body")]
    internal static partial int RequestSetBody(IntPtr req, IntPtr data, UIntPtr len);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_impersonate", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetImpersonate(IntPtr req, string? target, int defaultHeaders);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_proxy", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetProxy(IntPtr req, string? proxy, string? userpwd);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_follow_redirects")]
    internal static partial int RequestSetFollowRedirects(IntPtr req, int follow, long maxRedirects);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_timeout_ms")]
    internal static partial int RequestSetTimeoutMs(IntPtr req, long totalMs, long connectMs);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_accept_encoding", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetAcceptEncoding(IntPtr req, string? encoding);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_verify")]
    internal static partial int RequestSetVerify(IntPtr req, int peer, int host);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_cookie", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RequestSetCookie(IntPtr req, string? cookie);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_cookie_jar_enabled")]
    internal static partial int RequestSetCookieJarEnabled(IntPtr req, int enabled);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_verbose")]
    internal static partial int RequestSetVerbose(IntPtr req, int verbose);

    [LibraryImport(LibName, EntryPoint = "cidn_request_set_abort_flag")]
    internal static partial int RequestSetAbortFlag(IntPtr req, IntPtr flag);

    // ── Buffered execute ─────────────────────────────────────────────────
    [LibraryImport(LibName, EntryPoint = "cidn_session_execute")]
    internal static partial int SessionExecute(IntPtr session, IntPtr req, out CidnResponse response);

    [LibraryImport(LibName, EntryPoint = "cidn_response_free")]
    internal static partial void ResponseFree(ref CidnResponse resp);

    // ── Streaming execute ─────────────────────────────────────────────────
    // Kept as [DllImport] because the callback parameters are raw function pointers
    // passed as nint from delegate* unmanaged[Cdecl] in the caller.
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl,
               EntryPoint = "cidn_session_execute_stream")]
    internal static extern int SessionExecuteStream(
        IntPtr session,
        IntPtr req,
        nint writeCallback,
        nint bodyUserdata,
        nint headerCallback,
        nint headerUserdata,
        out long outStatus);

    [StructLayout(LayoutKind.Sequential)]
    internal struct CidnResponse
    {
        public long StatusCode;
        public IntPtr HeadersBlob;
        public IntPtr Body;
        public UIntPtr BodyLen;
        public IntPtr EffectiveUrl;
        public IntPtr Error;
    }
}
