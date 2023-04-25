# Design intent for v1 of PSCopilot

The goal for PSCopilot is to be the primary user experience for PowerShell users interacting with ChatGPT models.
We may separately train and provide domain specific models, but the focus is to enable an extensibility model
for partners and customers to leverage our user experience.

## Model extensibility

There are 3 primary scenarios we want to enable:

- Partner teams, like AzPowerShell, can provide tuned models that provide higher quality results for their domain
- Enterprise customers who want a model they control and can be trusted to send corporate data to
- General users who want to use the default model

The primary feature needed to enable this is a formal model registration system:

- A registration file, probably JSON, that contains:
  - The model name
  - The model endpoint
  - The model description
  - The name of the secret that contains the API key
  - Whether the model is trusted to send data to
- API keys are stored and retreived from SecretManagement

## User experience

Users would use a `Register-PSCopilotModel` cmdlet to register a model with parameters for the model name, endpoint,
description, and secret name.

A corresponding `Get-PSCopilotModel` cmdlet would return the registered models and `Unregister-PSCopilotModel` would
remove a model from the registry.

A `Set-PSCopilotModel` cmdlet would be needed to set the default as well as if a model is trusted.
Untrusted models would prompt the user to confirm sending data to the model.

### Partner team integration

AzPowerShell team, for example, could fine tune a model for Azure specific scenarios to provide higher quality results.
Their module can call `Register-PSCopilotModel` to register their model on behalf of their users.

May need to have a way with model registration to define the system prompt to use for the model.
For example, AzPowerShell may want to set the system context to include user's subscription and resource group type
information so that results automatically include those values for necessary parameters.

### Enterprise customers

Enterprise customers should not send their data to a model that they do not control.
We may need to work with the Azure OpenAI team to provide a way using AzPowerShell to easily get their
model information and register it with PSCopilot.

Enterprise customers may also want an easy way to know which model is being used (e.g. in the prompt)
and switch between models.

### General users

We will need to provide a free, but limited, model for Azure authenticated users to use to understand the value
and benefits of AI which should drive adoption of OpenAI in Azure with their own instance to alleviate privacy concerns.

Users would use `az login` or `Connect-AzAccount` to authenticate to Azure and then use `Register-PSCopilotKey` to store
their API key in SecretManagement.

An optional `-NewKey` switch would generate a new key and store it in SecretManagement and invalidate the old key.

## Telemetry

There will be both client side and server side telemetry.

### Client side telemetry

On the client side, we would like to collect:

- Number of models registered
- Features being used:
  - Chat
  - Error analysis
  - Test generation
  - Security analysis
  - Performance analysis
  - etc...

### Server side telemetry

The client can send a unique `User-Agent` header that identifies the node and/or session that is sending the request.
We would need to talk to the Azure OpenAI team to see if they can provide us with a way to correlate the requests
so that we know our client is driving their usage.

## Error analysis

Assuming a user has multiple models registered, it may make sense to allow ErrorRecords to include a model name
as additional context so that when using PSCopilot, it would automatically use that model to analyze the error
instead of the default one.
