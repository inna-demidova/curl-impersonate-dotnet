using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CurlImpersonate.Bindings.Interop;

public static class NativeLoader
{
    private static string? _upstreamLibPath;
    private static bool _initialized;
    private static readonly object _lock = new();

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255")]
    internal static void Register()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(NativeLoader).Assembly,
            ResolveNativeLibrary);
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "cidn_wrapper")
            return IntPtr.Zero;

        var rid = GetRid();
        var baseDir = AppContext.BaseDirectory;

        // Standard NuGet runtimes layout
        var candidates = new[]
        {
            Path.Combine(baseDir, "runtimes", rid, "native", GetWrapperFileName()),
            Path.Combine(baseDir, GetWrapperFileName()),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return NativeLibrary.Load(candidate);
        }

        // Fall back to default search
        if (NativeLibrary.TryLoad(GetWrapperFileName(), assembly, searchPath, out var handle))
            return handle;

        throw new DllNotFoundException(
            $"Cannot find cidn_wrapper native library for RID '{rid}'. " +
            $"Searched: {string.Join(", ", candidates)}");
    }

    /// <summary>
    /// Call once at startup. Locates the upstream libcurl-impersonate, loads it explicitly
    /// so the wrapper's dlopen/LoadLibrary receives an absolute path, then calls cidn_global_init.
    /// </summary>
    public static void Initialize(string? upstreamLibPath = null)
    {
        lock (_lock)
        {
            if (_initialized) return;

            _upstreamLibPath = upstreamLibPath ?? FindUpstreamLib();

            int rc = NativeMethods.GlobalInit(_upstreamLibPath);
            if (rc != 0)
            {
                var err = Marshal.PtrToStringAnsi(NativeMethods.LastError()) ?? "unknown error";
                throw new CurlImpersonateException($"cidn_global_init failed: {err}");
            }

            _initialized = true;
        }
    }

    public static void Cleanup()
    {
        lock (_lock)
        {
            if (!_initialized) return;
            NativeMethods.GlobalCleanup();
            _initialized = false;
        }
    }

    public static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    private static string FindUpstreamLib()
    {
        var rid = GetRid();
        var baseDir = AppContext.BaseDirectory;
        var nativeDir = Path.Combine(baseDir, "runtimes", rid, "native");

        var searchName = GetUpstreamLibFileName();

        // Exact filename first
        var exact = Path.Combine(nativeDir, searchName);
        if (File.Exists(exact)) return exact;

        // Glob for anything that looks like the upstream lib
        if (Directory.Exists(nativeDir))
        {
            foreach (var f in Directory.GetFiles(nativeDir, GetUpstreamLibGlob()))
                return f;
        }

        // Same dir as wrapper
        var sameDirExact = Path.Combine(baseDir, searchName);
        if (File.Exists(sameDirExact)) return sameDirExact;

        throw new FileNotFoundException(
            $"Cannot find upstream libcurl-impersonate library for RID '{rid}' in '{nativeDir}'. " +
            "Either place it there or pass the explicit path to NativeLoader.Initialize().");
    }

    private static string GetRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-arm64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "linux-x64" : "linux-arm64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return "unknown";
    }

    private static string GetWrapperFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cidn_wrapper.dll" : "libcidn_wrapper.so";

    private static string GetUpstreamLibFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "libcurl-impersonate.dll";
        // Linux: versioned soname, e.g. libcurl-impersonate-chrome.so.4
        return "libcurl-impersonate-chrome.so";
    }

    private static string GetUpstreamLibGlob()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "libcurl*.dll";
        return "libcurl-impersonate*.so*";
    }
}
