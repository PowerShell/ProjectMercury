using System;
using Azure;
using Azure.Core.Pipeline;

internal class AzureKeyCredentialPolicy : HttpPipelineSynchronousPolicy
{
    
    private readonly string _name;
    private readonly AzureKeyCredential _credential;
    private readonly string  _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyCredentialPolicy"/> class.
    /// </summary>
    /// <param name="credential">The <see cref="AzureKeyCredential"/> used to authenticate requests.</param>
    /// <param name="name">The name of the key header used for the credential.</param>
    /// <param name="prefix">The prefix to apply before the credential key. For example, a prefix of "SharedAccessKey" would result in
    /// a value of "SharedAccessKey {credential.Key}" being stamped on the request header with header key of <paramref name="name"/>.</param>
    public AzureKeyCredentialPolicy(AzureKeyCredential credential, string name, object prefix = null)
    {
        _credential = credential;
        _name = name;
        if(_prefix != null)
        {
            _prefix = (string) prefix;
        }
    }

    /// <inheritdoc/>
    public override void OnSendingRequest(Azure.Core.HttpMessage message)
    {
        base.OnSendingRequest(message);
        message.Request.Headers.SetValue(_name, _prefix != null ? $"{_prefix} {_credential.Key}" :  _credential.Key); 
    }
    public override void OnReceivedResponse(Azure.Core.HttpMessage message)
    {
        base.OnReceivedResponse(message);
        if (message.HasResponse == true && message.Response.Status == 429)
        {
            throw(new RateLimitException(message.Response.Content.ToString())); 
        }

    }
}

internal class RateLimitException : Exception
{
    public RateLimitException(string message) : base(message) { }

    public override string ToString()
    {
        return Message;
    }

    public override string StackTrace
    {
        get{return "";}
    }
}

