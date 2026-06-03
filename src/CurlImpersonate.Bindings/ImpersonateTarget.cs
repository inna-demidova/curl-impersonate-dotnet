namespace CurlImpersonate.Bindings;

/// <summary>
/// Strongly-typed impersonation target passed to <c>curl_easy_impersonate</c>.
/// The names below match the targets supported by the bundled curl-impersonate (lexiforest) v1.5.6
/// — i.e. the <c>--impersonate</c> aliases that ship with that release. If you bundle a different
/// upstream version, use <see cref="Custom"/> for targets not listed here.
/// </summary>
public sealed class ImpersonateTarget
{
    private ImpersonateTarget(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    // Chrome
    public static readonly ImpersonateTarget Chrome99        = new("chrome99");
    public static readonly ImpersonateTarget Chrome99Android = new("chrome99_android");
    public static readonly ImpersonateTarget Chrome100       = new("chrome100");
    public static readonly ImpersonateTarget Chrome101       = new("chrome101");
    public static readonly ImpersonateTarget Chrome104       = new("chrome104");
    public static readonly ImpersonateTarget Chrome107       = new("chrome107");
    public static readonly ImpersonateTarget Chrome110       = new("chrome110");
    public static readonly ImpersonateTarget Chrome116       = new("chrome116");
    public static readonly ImpersonateTarget Chrome119       = new("chrome119");
    public static readonly ImpersonateTarget Chrome120       = new("chrome120");
    public static readonly ImpersonateTarget Chrome123       = new("chrome123");
    public static readonly ImpersonateTarget Chrome124       = new("chrome124");
    public static readonly ImpersonateTarget Chrome131       = new("chrome131");
    public static readonly ImpersonateTarget Chrome131Android= new("chrome131_android");
    public static readonly ImpersonateTarget Chrome133a      = new("chrome133a");
    public static readonly ImpersonateTarget Chrome136       = new("chrome136");
    public static readonly ImpersonateTarget Chrome142       = new("chrome142");
    public static readonly ImpersonateTarget Chrome145       = new("chrome145");
    public static readonly ImpersonateTarget Chrome146       = new("chrome146");

    // Edge
    public static readonly ImpersonateTarget Edge99          = new("edge99");
    public static readonly ImpersonateTarget Edge101         = new("edge101");

    // Firefox
    public static readonly ImpersonateTarget Firefox133      = new("firefox133");
    public static readonly ImpersonateTarget Firefox135      = new("firefox135");
    public static readonly ImpersonateTarget Firefox144      = new("firefox144");
    public static readonly ImpersonateTarget Firefox147      = new("firefox147");

    // Safari
    public static readonly ImpersonateTarget Safari153       = new("safari153");
    public static readonly ImpersonateTarget Safari155       = new("safari155");
    public static readonly ImpersonateTarget Safari170       = new("safari170");
    public static readonly ImpersonateTarget Safari172Ios    = new("safari172_ios");
    public static readonly ImpersonateTarget Safari180       = new("safari180");
    public static readonly ImpersonateTarget Safari180Ios    = new("safari180_ios");
    public static readonly ImpersonateTarget Safari184       = new("safari184");
    public static readonly ImpersonateTarget Safari260       = new("safari260");
    public static readonly ImpersonateTarget Safari260Ios    = new("safari260_ios");

    // Tor
    public static readonly ImpersonateTarget Tor145          = new("tor145");

    /// <summary>Use any target string supported by the upstream library version.</summary>
    public static ImpersonateTarget Custom(string target) => new(target);
}
