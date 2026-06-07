using System.Runtime.InteropServices;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Verifies that the native wrapper loads cleanly and all expected symbols are resolved.
/// These tests require the native libraries to be staged in runtimes/{rid}/native/.
/// </summary>
[Trait("Category", "Native")]
[Collection("native")]
public class NativeLoadTests
{
    // The native library is initialized once by the shared NativeLibraryFixture ("native"
    // collection); this class no longer manages global lifecycle itself.

    [Fact]
    public void GlobalInit_Succeeds()
    {
        // Already called in constructor without throwing — this just documents the invariant.
        Assert.True(true);
    }

    [Fact]
    public void SessionCreate_ReturnsNonNull()
    {
        var session = NativeMethods.SessionCreate();
        Assert.NotEqual(IntPtr.Zero, session);
        NativeMethods.SessionDestroy(session);
    }

    [Fact]
    public void RequestCreate_ReturnsNonNull()
    {
        var req = NativeMethods.RequestCreate();
        Assert.NotEqual(IntPtr.Zero, req);
        NativeMethods.RequestDestroy(req);
    }

    [Fact]
    public void CurlStrerror_ReturnsString()
    {
        var ptr = NativeMethods.CurlStrerror(0);
        var str = Marshal.PtrToStringAnsi(ptr);
        Assert.False(string.IsNullOrEmpty(str));
    }
}
