# OpenAI Agent

This agent is designed to provide a flexible and user-friendly platform for interacting with OpenAI
services, either the public OpenAI service or a private deployment of the Azure OpenAI service,
through one or more customly defined GPT instances.

## GPT

GPTs are configured in the agent's settings file, which is in JSON format. Each GPT configuration
includes the name, description, the targeted OpenAI model, and the system prompt for interaction.
This allows for the creation of distinct GPTs, each tailored to a specific domain or scenario, whose
system prompts can be customized to suit these individual scenarios. Furthermore, you have the
flexibility to select different OpenAI models for each GPT as required.

## Command

The command `/gpt` is provided to make it easy to manage the GPTs.

- Run `/gpt use <gpt-name>` to switch to another GPT instance, or run `/gpt use` to simply choose
  from the available ones.
- Run `/gpt list <gpt-name>` to view the details of a GPT definition, or run `/gpt list` to list all
  available GPTs.

```shell
aish:1> /gpt --help
Description:
  Command for GPT management within the 'openai-gpt' agent.

Usage:
  gpt [command] [options]

Options:
  -h, --help  Show help and usage information

Commands:
  list <GPT>  List a specific GPT, or all available GPTs.
  use <GPT>   Specify a GPT to use, or choose one from the available GPTs.
```

## Prerequisites

- For OpenAI, you need the **Model Name** and **API Key** to use the agent.
  - [OpenAI API Key][03]
  - [OpenAI Model][04]

- For Azure OpenAI Service, you need the **Endpoint**, **Deployment Name**, **Model Name**, and **API Key** to use the agent.
  - [Access to Azure OpenAI][01]
  - [Create an Azure OpenAI deployment][02]

## Configuration

To configure the agent, run `/agent config openai-gpt` to open up the setting file in your default editor,
and then update the file based on the following example.

```jsonc
{
  // Declare GPT instances.
  "GPTs": [
    // To use the Azure OpenAI service:
    // - Set `Endpoint` to the endpoint of your Azure OpenAI service,
    //     or the endpoint to the Azure API Management service if you are using it as a gateway.
    // - Set `Deployment` to the deployment name of your Azure OpenAI service.
    // - Set `ModelName` to the name of the model used for your deployment, e.g. "gpt-4-0613".
    // - Set `Key` to the access key of your Azure OpenAI service,
    //     or the key of the Azure API Management service if you are using it as a gateway.
    {
      "Name": "ps-az-gpt4",
      "Description": "A GPT instance with expertise in PowerShell scripting and command line utilities. Use gpt-4 running in Azure.",
      "Endpoint": "<insert your Azure OpenAI endpoint>",
      "Deployment": "<insert your deployment name>",
      "ModelName": "<insert the model name>",   // required field to infer properties of the service, such as token limit.
      "Key": "<insert your key>",
      "SystemPrompt": "1. You are a helpful and friendly assistant with expertise in PowerShell scripting and command line.\n2. Assume user is using the operating system `Windows 11` unless otherwise specified.\n3. Use the `code block` syntax in markdown to encapsulate any part in responses that is code, YAML, JSON or XML, but not table.\n4. When encapsulating command line code, use '```powershell' if it's PowerShell command; use '```sh' if it's non-PowerShell CLI command.\n5. When generating CLI commands, never ever break a command into multiple lines. Instead, always list all parameters and arguments of the command on the same line.\n6. Please keep the response concise but to the point. Do not overexplain."
    },

    // To use the public OpenAI service:
    // - Ignore the `Endpoint` and `Deployment` keys.
    // - Set `ModelName` to the name of the model to be used.
    // - Set `Key` to be the OpenAI access token.
    // For example:
    {
        "Name": "ps-gpt4o",
        "Description": "A GPT instance with expertise in PowerShell scripting and command line utilities. Use gpt-4o running in OpenAI.",
        "ModelName": "gpt-4o",
        "Key": "<insert your key>",
        "SystemPrompt": "1. You are a helpful and friendly assistant with expertise in PowerShell scripting and command line.\n2. Assume user is using the operating system `Windows 11` unless otherwise specified.\n3. Use the `code block` syntax in markdown to encapsulate any part in responses that is code, YAML, JSON or XML, but not table.\n4. When encapsulating command line code, use '```powershell' if it's PowerShell command; use '```sh' if it's non-PowerShell CLI command.\n5. When generating CLI commands, never ever break a command into multiple lines. Instead, always list all parameters and arguments of the command on the same line.\n6. Please keep the response concise but to the point. Do not overexplain."
    }
  ],

  // Specify the default GPT instance to use for user query.
  "Active": "ps-az-gpt4"
}
```

<!-- link references -->
[01]: https://aka.ms/oai/access?azure-portal=true
[02]: https://learn.microsoft.com/azure/ai-services/openai/how-to/create-resource?pivots=web-portal
[03]: https://platform.openai.com/api-keys
[04]: https://platform.openai.com/docs/models
