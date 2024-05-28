# OpenAI Agent

This agent talks to OpenAI or your own deployment of Azure OpenAI.

## Prerequisites

### OpenAI

- [OpenAI API Key][03]

### Azure OpenAI Service

- [Access to Azure OpenAI][01]
- [Create an Azure OpenAI deployment][02]

You will need the following information to use the agent:

- Azure OpenAI Endpoint
- Azure OpenAI Deployment Name
- Azure OpenAI API Key

## Configuration

### GPTs

A GPT is a registered instance of an LLM that's configured for a specific use case. For example, you
might want to have two different GPTs: one that's a PowerShell expert and another that's a Python
expert. GPTs are defined in the agent config file.

To configure the agent, run `/agent config openai-gpt` to open up the configuration file in your
default editor. The default sample config file contains the following:

```json
{
  // Declare GPT instances.
  "GPTs": [
    // To use Azure OpenAI as the AI completion service:
    // - Set `Endpoint` to the endpoint of your Azure OpenAI service,
    //   or the endpoint to the Azure API Management service if you are using it as a gateway.
    // - Set `Deployment` to the deployment name of your Azure OpenAI service.
    // - Set `Key` to the access key of your Azure OpenAI service,
    //   or the key of the Azure API Management service if you are using it as a gateway.
    {
      "Name": "powershell-ai",
      "Description": "A GPT instance with expertise in PowerShell scripting and command line utilities.",
      "Endpoint": "<insert my Azure OpenAI endpoint>",
      "Deployment": "gpt4",
      "ModelName": "gpt-4-0314",   // required field to infer properties of the service, such as token limit.
      "Key": "<insert your key>",
      "SystemPrompt": "You are a helpful and friendly assistant with expertise in PowerShell scripting and command line.\nAssume user is using the operating system `osx` unless otherwise specified.\nPlease always respond in the markdown format and use the `code block` syntax to encapsulate any part in responses that is longer-format content such as code, YAML, JSON, and etc."
    },

    // To use the public OpenAI as the AI completion service:
    // - Ignore the `Endpoint` and `Deployment` keys.
    // - Set `Key` to be the OpenAI access token.
    // For example:
    /*
    {
        "Name": "python-ai",
        "Description": "A GPT instance that acts as an expert in python programming that can generate python code based on user's query.",
        "ModelName": "gpt-4",
        "Key": null,
        "SystemPrompt": "example-system-prompt"
    }
    */
  ],

  // Specify the GPT instance to use for user query.
  "Active": "powershell-ai"
}
```

If you have defined multiple GPTs, you can switch between them by running `/gpt use <GPT Name>`. To
see a list of available GPTs, run `/gpt list`.

<!-- link references -->
[01]: https://aka.ms/oai/access?azure-portal=true
[02]: https://learn.microsoft.com/azure/ai-services/openai/how-to/create-resource?pivots=web-portal
[03]: https://platform.openai.com/api-keys
