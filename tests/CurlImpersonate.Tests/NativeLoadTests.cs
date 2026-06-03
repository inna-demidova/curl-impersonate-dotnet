using System.Runtime.InteropServices;
using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Verifies that the native wrapper loads cleanly and all expected symbols are resolved.
/// These tests require the native libraries to be staged in runtimes/{rid}/native/.
/// </summary>
[Trait("Category", "Native")]
public class NativeLoadTests : IDisposable
{
    public NativeLoadTests()
    {
        // NativeLoader.Initialize() will find the upstream lib from runtimes/{rid}/native/
        NativeLoader.Initialize();
    }

    public void Dispose() => NativeLoader.Cleanup();

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
