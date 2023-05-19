## Private preview scenarios

Target scenarios:

- Allow to use existing Azure OpenAI service deployment or the public OpenAI service;
- Allow to create Azure OpenAI service account and deploy models to it;
- Enable interactive chat session in terminal with appealing UX;

Is this also a target?

- Provide a public facing endpoint that is backed by a PowerShell-team owned Azure OpenAI service deployment
  - Allow a user to try out the command-line experience for free

## Feature list based on scenarios

1. Login support -- user can choose whether or not to login Azure.

   - If the user has logged in with az cli, is it possible for our exe to get access too?

1. Endpoint registraion

   - Ideally, when logged in, we should be able to auto-discover existing Azure OpenAI deployments,
     and auto-register the ones approved by the user (prompt to ask for approval).
     Instead of using API key in this case, we probably will be using the AAD token (**how?** need research).

   - When not logged in, what data is needed for the registration?
     - Azure OpenAI: `endpoint URL`, `api-key`, `model`
     - Public OpenAI: `api-key`, `model`

   - What to store for registrations?
     - Data stored may be different for logged-in vs. not-logged-in cases (**how different?**)
       - For registration done by auto-registration in logged-in case,
         would it be possible for user to use the registration without logging in later on?
     - Location `HOME\.azcopilot`, api-key stored in file in plain-text.

   - registration management
     - `new` create a new endpoint (Azure OpenAI deployment), login required.
     - `add` an existing endpoint
     - `list` registered endpoints
     - `set` a property of an endpoint, such as URL, api-key, or models

1. Create cognitive service account and deploy Azure.OpenAI

   - Both Az CLI and Azure PowerShell can do this.
     ```pwsh
     PS:11> az cognitiveservices account deployment list -g OpenAI -n powershell-openai | ConvertFrom-Json | % name
     gpt-35-turbo
     gpt4
     gpt4-32k
     PS:12> az cognitiveservices account deployment --help

     Group
         az cognitiveservices account deployment : Manage deployments for Azure Cognitive Services
         accounts.

     Commands:
         create : Create a deployment for Azure Cognitive Services account.
         delete : Delete a deployment from Azure Cognitive Services account.
         list   : Show all deployments for Azure Cognitive Services account.
         show   : Show a deployment for Azure Cognitive Services account.
     ```

   - The relevant [Azure PowerShell module](https://github.com/Azure/azure-powershell/tree/main/src/CognitiveServices/CognitiveServices.Management.Sdk) doesn't use a management-plan library, instead it auto-generates the management sdk using `AutoRest`.

   - I found the NuGet package [Azure.ResourceManager.CognitiveServices](https://www.nuget.org/packages/Azure.ResourceManager.CognitiveServices/#versions-body-tab), which looks to me is the SDK for managing Azure Cognitive Services resources.
     - [API references URL](https://learn.microsoft.com/en-us/dotnet/api/azure.resourcemanager.cognitiveservices?view=azure-dotnet)
     - [Cognitive Services management client library ReadMe](https://github.com/Azure/azure-sdk-for-net/tree/Azure.ResourceManager.CognitiveServices_1.2.1/sdk/cognitiveservices/Azure.ResourceManager.CognitiveServices)

1. Communication with OpenAI endpoints

   - Use the NuGet package [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/1.0.0-beta.5#readme-body-tab) for both Azure OpenAI service and the public OpenAI service.
     - [Azure.AI.OpenAI ReadMe](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI)
     - [Samples using this SDK](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI/tests/Samples)
     - [Lifetime management for Azure SDK .NET clients](https://devblogs.microsoft.com/azure-sdk/lifetime-management-and-thread-safety-guarantees-of-azure-sdk-net-clients/)

1. Terminal side: readline experience

   - For intern project, we can use a simple read-line implementation just like what `PSCopilot` already has.

   - Eventually, we want to use the PSReadLine key processing component, but need to strip off the PowerShell specific logic.
     This will allow us to provide the same read-line experience as in PowerShell with the Windows, Emacs, and VI modes.

1. Terminal side: interactive chat

   - Need to design the UX for the interactive chat in terminal
   - Need syntax highlighting for the code snippet returned from AI.
     - Use the NuGet package [ColorCode.Core](https://www.nuget.org/packages/ColorCode.Core).
       [GitHub Repo](https://github.com/CommunityToolkit/ColorCode-Universal)
     - We need to implement the formatter targeting terminal. I have done prototype, which proves the `ColorCode` package is easy to extend both the formatter and a new language regex parser.


If we want to provide a public facing endpoint for users to try out without endpoint registration,
then we will need the following feature work.

- Use API Management Service as the gateway for the PS-team Azure OpenAI endpoints
  - API management service has rate limit, based on IP for example.
  - It can use named values that references Azure KeyVault secret. So we can store our api key in key vault.
  - It can set header using secret fro in-bound request. So we can add the api key when forwarding the request to our OpenAI instance.

- However, when using API management service, we won't be able to use the NuGet package `Azure.AI.OpenAI` (_OpenAI Client_) to access our OpenAI instances, but have to do HTTP calls directly.
  - It makes a different code path comparing to talking to the registered endpoints, but I think that's fine.
