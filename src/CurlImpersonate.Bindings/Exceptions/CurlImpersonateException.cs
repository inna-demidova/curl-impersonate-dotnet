namespace CurlImpersonate.Bindings;

public sealed class CurlImpersonateException : Exception
{
    public int CurlCode { get; }
    public string CurlErrorMessage { get; }

    public CurlImpersonateException(int curlCode, string curlErrorMessage)
        : base($"curl error {curlCode}: {curlErrorMessage}")
    {
        CurlCode = curlCode;
        CurlErrorMessage = curlErrorMessage;
    }

    public CurlImpersonateException(string message) : base(message)
    {
        CurlCode = -1;
        CurlErrorMessage = message;
    }
}
