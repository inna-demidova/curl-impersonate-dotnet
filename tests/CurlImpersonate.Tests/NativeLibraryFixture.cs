using CurlImpersonate.Bindings.Interop;

namespace CurlImpersonate.Tests;

/// <summary>
/// Initializes the native library exactly once for the whole test run and cleans it up once at the
/// end. Shared via the "native" collection so the global libcurl state is never torn down while
/// another test class is still mid-transfer (xUnit parallelizes across collections by default).
/// </summary>
public sealed class NativeLibraryFixture : IDisposable
{
    public NativeLibraryFixture() => NativeLoader.Initialize();

    public void Dispose() => NativeLoader.Cleanup();
}

[CollectionDefinition("native")]
public sealed class NativeCollection : ICollectionFixture<NativeLibraryFixture>
{
    // Marker type only — no code. Applying [Collection("native")] to a test class makes xUnit
    // inject the shared NativeLibraryFixture and run all such classes serially in one collection.
}
