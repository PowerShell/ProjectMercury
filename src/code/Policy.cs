using System;
using Azure;
using Azure.Core.Pipeline;

internal class AzureKeyCredentialPolicy : HttpPipelineSynchronousPolicy
{
    
    private readonly string _name;
    private readonly AzureKeyCredential _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyCredentialPolicy"/> class.
    /// </summary>
    /// <param name="credential">The <see cref="AzureKeyCredential"/> used to authenticate requests.</param>
    /// <param name="name">The name of the key header used for the credential.</param>

    public AzureKeyCredentialPolicy(AzureKeyCredential credential, string name)
    {
        _credential = credential;
        _name = name;
    }

    /// <inheritdoc/>
    public override void OnSendingRequest(Azure.Core.HttpMessage message)
    {
        base.OnSendingRequest(message);
        message.Request.Headers.SetValue(_name, _credential.Key); 
    }
}


