using System;
using Azure;
using Azure.Core.Pipeline;

internal class ApimSubscriptionKeyPolicy : HttpPipelineSynchronousPolicy
{
    private const string Header = "Ocp-Apim-Subscription-Key";
    private readonly AzureKeyCredential _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApimSubscriptionKeyPolicy"/> class.
    /// </summary>
    /// <param name="credential">The <see cref="AzureKeyCredential"/> used to authenticate requests.</param>

    public ApimSubscriptionKeyPolicy(AzureKeyCredential credential)
    {
        _credential = credential;
    }

    /// <inheritdoc/>
    public override void OnSendingRequest(Azure.Core.HttpMessage message)
    {
        base.OnSendingRequest(message);
        message.Request.Headers.SetValue(Header, _credential.Key); 
    }
}
