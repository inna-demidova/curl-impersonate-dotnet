using CurlImpersonate.Bindings;
using CurlImpersonate.Bindings.Interop;

// Initialize native loader (finds libcurl-impersonate from runtimes/{rid}/native/).
NativeLoader.Initialize();

Console.WriteLine("=== curl-impersonate-dotnet sample ===");
Console.WriteLine();

await using var client = new ImpersonateClient(new ClientOptions
{
    DefaultTarget   = ImpersonateTarget.Chrome131,
    TimeoutMs       = 15_000,
    ConnectTimeoutMs = 5_000,
    CookieJarEnabled = true,
});

// 1. Basic GET
Console.WriteLine("-- GET https://httpbin.org/get");
var resp = await client.GetAsync("https://httpbin.org/get");
Console.WriteLine($"   Status: {(int)resp.StatusCode} {resp.StatusCode}");
Console.WriteLine($"   Effective URL: {resp.EffectiveUrl}");
Console.WriteLine($"   Body snippet: {resp.GetBodyAsString()[..Math.Min(200, resp.Body.Length)]}");
Console.WriteLine();

// 2. TLS fingerprint check
Console.WriteLine("-- GET https://tls.peet.ws/api/all  (fingerprint check)");
var tlsResp = await client.GetAsync("https://tls.peet.ws/api/all");
Console.WriteLine($"   Status: {(int)tlsResp.StatusCode}");
var body = tlsResp.GetBodyAsString();
var ja3Start = body.IndexOf("\"ja3\":");
if (ja3Start >= 0)
    Console.WriteLine($"   JA3 excerpt: {body.Substring(ja3Start, Math.Min(80, body.Length - ja3Start))}");
Console.WriteLine();

// 3. POST
Console.WriteLine("-- POST https://httpbin.org/post");
var postResp = await client.PostAsync("https://httpbin.org/post", """{"key":"value"}""", "application/json");
Console.WriteLine($"   Status: {(int)postResp.StatusCode}");
Console.WriteLine();

Console.WriteLine("Done.");
NativeLoader.Cleanup();
