## Private preview 1

### Intern project scenarios

Target scenarios for the intern project:

- Provide a public facing endpoint that is backed by a PowerShell-team owned Azure OpenAI service deployment
  - Allow a user to try out the command-line experience for free
- Enable interactive chat session in terminal with appealing UX in terminal
- Deploy in CloudShell bash and PowerShell

### Feature list based on scenarios

1. Use API Management Service (APIM) as the gateway for the PS-team Azure OpenAI endpoints
   - API management service has rate limit, based on IP for example.
   - It can use named values that references Azure KeyVault secret. So we can store our api key in key vault.
   - It can set header using secret fro in-bound request. So we can add the api key when forwarding the request to our OpenAI instance.

1. Use the [OpenAPI Swagger spec](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/data-plane/AzureOpenAI/inference/preview/2023-03-15-preview/inference.json) to create APIs in APIM.
   - It's confirmed doable to use the [Azure.AI.OpenAI][aoai-nuget] NuGet package to talk to the APIM gateway endpoint.
   - When using the `OpenAIClient` talking to APIM endpoint, we use a placeholder for the API key.
   - Set policy in APIM to override the `api-key` header for the in-coming request, with the real Azure OpenAI api key stored in key vault.

1. Terminal side: readline experience
   - For intern project, we can use a simple read-line implementation just like what `PSCopilot` already has.
   - Eventually, we want to use the PSReadLine key processing component, but need to strip off the PowerShell specific logic.
     This will allow us to provide the same read-line experience as in PowerShell with the Windows, Emacs, and VI modes.

1. Terminal side: interactive chat
   - Support using both the main screen buffer and alternate screen buffer
   - Need to design the UX for the interactive chat in terminal
     - When using the alternate buffer, change the default background color to differentiate from main screen buffer
     - Need to make it easy to indicate which message is from AI and which is from user.
     - Need to parse the response text with markdown parser and render accordingly (e.g. bold effect)
       - think about an AI-model for generating "specification"
       - the Bing system prompt asks AI to encapsulate relevant text in bold in markdown
       - this is where we **may** use `Spectre.Console`, like for table rendering
     - syntax highlighting for the code snippet in response.
       - Use the NuGet package [ColorCode.Core](https://www.nuget.org/packages/ColorCode.Core).
       [GitHub Repo](https://github.com/CommunityToolkit/ColorCode-Universal)
       - We need to implement the formatter targeting terminal. I have done prototype, which proves the `ColorCode` package is easy to extend both the formatter and a new language regex parser.
     - ... (**more??**)
   - Need to design the interaction between `AzCopilot` and user's main shell
     - how to pass data to `AzCopilot`
     - how to select the data from AI response and use in user's shell
     - ...

## Private preview 2 and beyond

### scenarios

- Automatically create Azure Cognitive Services account and deploy Azure.OpenAI instances after user approval
- Allow to use existing Azure OpenAI service deployment
  - For the public OpenAI service, it's not a goal for now. The objective is to drive Azure.OpenAI usage.
- AI-model management
  - The term "_AI-model_" is an abstract concept, representing a specific AI usage scenario. For example:
    - "Azure PowerShell" AI-model
    - "Bash and Az CLI" AI-model
    - "PowerShell scripting" AI-model
  - An "_AI-model_" consists of 2 components: an OpenAI endpoint, a system prompt.
  - An "_OpenAI endpoint_" consists of 3 parts: endpoint URL, api-key OR AAD token, deployment name.

### Feature list based on scenarios

1. Login support -- user can choose whether or not to login Azure.

   - `Azure.Identity` for Azure authentication
   - If the user has logged in with az cli, is it possible for our exe to get access too?
     - Or, if it's used in Cloud Shell, is it possible to get access without authentication?

1. AI-model management

   - **Need a demo.ps1 script to show how the AI-model registraion scenarios work.**
     - dev design depends on the demo script to show how the AI-model management works.

   - **OLD** endpoint registration management (may not apply to the AI-model management)
     - Ideally, when logged in, we should be able to auto-discover existing Azure OpenAI deployments,
     and auto-register the ones approved by the user (prompt to ask for approval).
     Instead of using API key in this case, we probably will be using the AAD token (**how?** need research).

    - When not logged in, what data is needed for the registration?
      - Azure OpenAI: `endpoint URL`, `api-key`, `deployment` (e.g. gpt4, gpt3.5-turbo)
      - Public OpenAI: `api-key`, `deployment`

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
     - Tag the Azure OpenAI instance with our project name when creating it.

1. Communication with OpenAI endpoints
   - We are going to use the NuGet package [Azure.AI.OpenAI][aoai-nuget] -- `OpenAIClient` for both real Azure OpenAI service endpoint and the free APIM endpoint.
     - [Azure.AI.OpenAI ReadMe](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI)
     - [Samples using this SDK](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI/tests/Samples)
     - [Lifetime management for Azure SDK .NET clients](https://devblogs.microsoft.com/azure-sdk/lifetime-management-and-thread-safety-guarantees-of-azure-sdk-net-clients/)

[aoai-nuget]: https://www.nuget.org/packages/Azure.AI.OpenAI/1.0.0-beta.5#readme-body-tab
