using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Shell
{
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
}
