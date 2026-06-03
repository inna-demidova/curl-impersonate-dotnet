namespace CurlImpersonate.Bindings;

/// <summary>Strongly-typed impersonation target passed to curl_easy_impersonate.</summary>
public sealed class ImpersonateTarget
{
    private ImpersonateTarget(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    // Chrome
    public static readonly ImpersonateTarget Chrome99      = new("chrome99");
    public static readonly ImpersonateTarget Chrome100     = new("chrome100");
    public static readonly ImpersonateTarget Chrome101     = new("chrome101");
    public static readonly ImpersonateTarget Chrome104     = new("chrome104");
    public static readonly ImpersonateTarget Chrome107     = new("chrome107");
    public static readonly ImpersonateTarget Chrome110     = new("chrome110");
    public static readonly ImpersonateTarget Chrome116     = new("chrome116");
    public static readonly ImpersonateTarget Chrome119     = new("chrome119");
    public static readonly ImpersonateTarget Chrome120     = new("chrome120");
    public static readonly ImpersonateTarget Chrome123     = new("chrome123");
    public static readonly ImpersonateTarget Chrome124     = new("chrome124");
    public static readonly ImpersonateTarget Chrome126     = new("chrome126");
    public static readonly ImpersonateTarget Chrome127     = new("chrome127");
    public static readonly ImpersonateTarget Chrome128     = new("chrome128");
    public static readonly ImpersonateTarget Chrome129     = new("chrome129");
    public static readonly ImpersonateTarget Chrome130     = new("chrome130");
    public static readonly ImpersonateTarget Chrome131     = new("chrome131");

    // Firefox
    public static readonly ImpersonateTarget Firefox99     = new("ff99");
    public static readonly ImpersonateTarget Firefox100    = new("ff100");
    public static readonly ImpersonateTarget Firefox102    = new("ff102");
    public static readonly ImpersonateTarget Firefox104    = new("ff104");
    public static readonly ImpersonateTarget Firefox105    = new("ff105");
    public static readonly ImpersonateTarget Firefox106    = new("ff106");
    public static readonly ImpersonateTarget Firefox108    = new("ff108");
    public static readonly ImpersonateTarget Firefox110    = new("ff110");
    public static readonly ImpersonateTarget Firefox117    = new("ff117");
    public static readonly ImpersonateTarget Firefox120    = new("ff120");
    public static readonly ImpersonateTarget Firefox123    = new("ff123");
    public static readonly ImpersonateTarget Firefox124    = new("ff124");
    public static readonly ImpersonateTarget Firefox128    = new("ff128");
    public static readonly ImpersonateTarget Firefox133    = new("ff133");

    // Safari
    public static readonly ImpersonateTarget Safari15_3    = new("safari15_3");
    public static readonly ImpersonateTarget Safari15_5    = new("safari15_5");
    public static readonly ImpersonateTarget Safari17_0    = new("safari17_0");
    public static readonly ImpersonateTarget Safari17_2_1  = new("safari17_2_1");
    public static readonly ImpersonateTarget Safari18_0    = new("safari18_0");

    // Edge
    public static readonly ImpersonateTarget Edge99        = new("edge99");
    public static readonly ImpersonateTarget Edge101       = new("edge101");

    /// <summary>Use any target string supported by the upstream library version.</summary>
    public static ImpersonateTarget Custom(string target) => new(target);
}