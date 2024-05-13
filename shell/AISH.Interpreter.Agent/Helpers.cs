using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace AISH.Interpreter.Agent;

/// <summary>
/// Static type that contains all utility methods.
/// </summary>
internal static class Utils
{
    internal const string ApimAuthorizationHeader = "Ocp-Apim-Subscription-Key";
    internal const string ApimGatewayDomain = ".azure-api.net";
    internal const string AzureOpenAIDomain = ".openai.azure.com";
    internal const string AISHEndpoint = "https://pscopilot.azure-api.net";
    internal const string OpenAIEndpoint = "https://api.openai.com";
    internal const string KeyApplicationHelpLink = "https://github.com/PowerShell/AISH#readme";

    internal static readonly string OS;

    static Utils()
    {
        string rid = RuntimeInformation.RuntimeIdentifier;
        int index = rid.IndexOf('-');
        OS = index is -1 ? rid : rid[..index];
    }

    internal static string ConvertFromSecureString(SecureString secureString)
    {
        if (secureString is null || secureString.Length is 0)
        {
            return null;
        }

        nint ptr = IntPtr.Zero;

        try
        {
            ptr = Marshal.SecureStringToBSTR(secureString);
            return Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }

    internal static SecureString ConvertToSecureString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var ss = new SecureString();
        foreach (char c in text)
        {
            ss.AppendChar(c);
        }

        return ss;
    }

    internal static bool IsEqualTo(this SecureString ss1, SecureString ss2)
    {
        if (ss1.Length != ss2.Length)
        {
            return false;
        }

        if (ss1.Length is 0)
        {
            return true;
        }

        string plain1 = ConvertFromSecureString(ss1);
        string plain2 = ConvertFromSecureString(ss2);
        return string.Equals(plain1, plain2, StringComparison.Ordinal);
    }
}

/// <summary>
/// <see cref="SecureString"/> converter for JSON serialization/de-serialization.
/// </summary>
internal class SecureStringJsonConverter : JsonConverter<SecureString>
{
    public override SecureString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string payload = reader.GetString();
        return Utils.ConvertToSecureString(payload);
    }

    public override void Write(Utf8JsonWriter writer, SecureString value, JsonSerializerOptions options)
    {
        string payload = Utils.ConvertFromSecureString(value);
        writer.WriteStringValue(payload);
    }
}

#nullable enable

/// <summary>
/// Used for setting user key for the Azure.OpenAI.Client.
/// </summary>
internal sealed class UserKeyPolicy : HttpPipelineSynchronousPolicy
{
    private readonly string _name;
    private readonly AzureKeyCredential _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserKeyPolicy"/> class.
    /// </summary>
    /// <param name="credential">The <see cref="AzureKeyCredential"/> used to authenticate requests.</param>
    /// <param name="name">The name of the key header used for the credential.</param>
    public UserKeyPolicy(AzureKeyCredential credential, string name)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrEmpty(name);

        _credential = credential;
        _name = name;
    }

    /// <inheritdoc/>
    public override void OnSendingRequest(HttpMessage message)
    {
        base.OnSendingRequest(message);
        message.Request.Headers.SetValue(_name, _credential.Key);
    }
}

/// <summary>
/// Used for configuring the retry policy for Azure.OpenAI.Client.
/// </summary>
internal sealed class ChatRetryPolicy : RetryPolicy
{
    private const string RetryAfterHeaderName = "Retry-After";
    private const string RetryAfterMsHeaderName = "retry-after-ms";
    private const string XRetryAfterMsHeaderName = "x-ms-retry-after-ms";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatRetryPolicy"/> class.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries to attempt.</param>
    /// <param name="delayStrategy">The delay to use for computing the interval between retry attempts.</param>
    public ChatRetryPolicy(int maxRetries = 2, DelayStrategy? delayStrategy = default) : base(
        maxRetries,
        delayStrategy ?? DelayStrategy.CreateExponentialDelayStrategy(
            initialDelay: TimeSpan.FromSeconds(0.8),
            maxDelay: TimeSpan.FromSeconds(5)))
    {
        // By default, we retry 2 times at most, and use a delay strategy that waits 5 seconds at most between retries.
    }

    protected override bool ShouldRetry(HttpMessage message, Exception? exception) => ShouldRetryImpl(message, exception);
    protected override ValueTask<bool> ShouldRetryAsync(HttpMessage message, Exception? exception) => new(ShouldRetryImpl(message, exception));

    private bool ShouldRetryImpl(HttpMessage message, Exception? exception)
    {
        bool result = base.ShouldRetry(message, exception);

        if (result && message.HasResponse)
        {
            TimeSpan? retryAfter = GetRetryAfterHeaderValue(message.Response.Headers);
            if (retryAfter > TimeSpan.FromSeconds(5))
            {
                // Do not retry if the required interval is longer than 5 seconds.
                return false;
            }
        }

        return result;
    }

    private static TimeSpan? GetRetryAfterHeaderValue(ResponseHeaders headers)
    {
        if (headers.TryGetValue(RetryAfterMsHeaderName, out var retryAfterValue) ||
            headers.TryGetValue(XRetryAfterMsHeaderName, out retryAfterValue))
        {
            if (int.TryParse(retryAfterValue, out var delaySeconds))
            {
                return TimeSpan.FromMilliseconds(delaySeconds);
            }
        }

        if (headers.TryGetValue(RetryAfterHeaderName, out retryAfterValue))
        {
            if (int.TryParse(retryAfterValue, out var delaySeconds))
            {
                return TimeSpan.FromSeconds(delaySeconds);
            }

            if (DateTimeOffset.TryParse(retryAfterValue, out DateTimeOffset delayTime))
            {
                return delayTime - DateTimeOffset.Now;
            }
        }

        return default;
    }
}
