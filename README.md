# Shell Copilot

This is a repository of various A.I + Shell prototypes we have created to test out experiences and
features. **Shell Copilot** is the latest and most finished prototype. It is a CLI tool that creates
an interactive chat session with a registered Large Language Model. Currently we are in a **Private Preview** state and everything is subject to change.

![GIF showing demo of Shell Copilot](./docs/media/ShellCopilotDemo.gif)

## Installing and Using Shell Copilot

Here are the steps to install and use Shell Copilot.
1. Clone this repository
2. To build run `./build.ps1` in the project's directory
3. Add the `<path to project>\ShellCopilot\out\debug` directory to your `$PATH` with `$env:PATH += <path to project>\ShellCopilot\out\debug`
4. Add the above line to your `$PROFILE` to be able to use it anytime you open up PowerShell. You can edit it by doing `code $PROFILE` if you have VSCode installed.

> Note: Depending on your OS directory paths may be `\` on Windows or `/` on Mac.

## Getting an Azure OpenAI Endpoint Key

Currently we only support Azure OpenAI LLM endpoints. We are currently hosting a internal only Azure
OpenAI endpoint that you can get and use without getting your Azure OpenAI instance. This is for private preview purposes only.

Guide for Signing Up For API Key
1.  Navigate to <https://pscopilot.developer.azure-api.net>
2.  Click `Sign Up` located on the top right corner of the page.
3.  Sign up for a subscription by filling in the fields (email, password, first name, last name).
4.  Verify the account (An email should have been sent from
    <apimgmt-noreply@mail.windowsazure.com> to your email)
5.  Click `Sign In` located on the top right corner of the page.
6.  Enter the email and password used when signing up.
7.  Click `Products` located on the top right corner of the page
8.  In the field stating `Your new product subscription name`, Enter `Azure OpenAI Service API`.
9.  Click `Subscribe` to be subscribed to the product.

In order to view your subscription/API key,
1.  Click `Profile` located on the top right corner of the page.
2.  Your Key should be located under the `Subscriptions` section. Click on `Show` to view the
    primary or secondary key.

To register an endpoint you can use the `aish register` subcommand.

```console
aish register --name <Name Of Model> --endpoint https://pscopilot.azure-api.net --key <Insert Key From Above Steps> --deployment gpt4 --openai-model gpt-4-0314 --system-prompt <Add Whatever System Prompt you want to guide the LLM>
```
Here are better descriptions for the arguments for this command:
- `--name` is your choice of name for the model registration, we suggest using something that is easily identifiable. Something like `default-model` could work or if you tailor the model to be a pro in python a name could be `python-model` etc. 
- `--endpoint` currently we only support Azure OpenAI service endpoints but for the sake of the early private preview you can use `https://pscopilot.azure-api.net` and get the key from instructions above.
- `--key` is the key you get from [steps above](#Getting-an-Azure-OpenAI-Endpoint-key)
- `--deployment` is the name of the model being used in the backend, if you are using the private preview endpoint, be sure its value is `gpt4`
- `--openai-model` is the name of openai endpoint and version of it,  if you are using the private preview endpoint, the correct version is `gpt-4-0314`
- `--SystemPrompt` is a simple system prompt or grounding mechanism for the calls to the endpoint, it can help guide the model to be specific to an area. We suggest doing something like:

```
You are an AI assistant with expertise in PowerShell, Azure, and the command line. Assume user is using the operating system "<INSERT OS>" unless otherwise specified. You are helpful, creative, clever, and very friendly. You always respond in the markdown format. You use the "code blocks" syntax from markdown to encapsulate any part in responses that's longer-format content such as code, poem, lyrics, etc
```

There are two other non-required flags you can use:
- `--description` is a description of the custom AI model
- `--trust-level` can be either `Public` or `Private`, current there is no difference between the two but eventually we see this being useful for models that are public models or models that are private to an enterprise customer.

If you have separate Azure OpenAI endpoint you can use that instead of the one above. Read more at
[Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource?pivots=ps).

## Using Shell Copilot

To start a chat session with the LLM, simply run `aish` and it will open up a new session in your current window. You can also use `aish --use-alt-buffer` to open up a new chat session in the alternate screen buffer. 

To explore the other options available to you, run `aish --help` to see all the subcommands.

## Feedback

We still in development and value any and all feedback! Please file an [issue in this repository](https://github.com/PowerShell/ShellCopilot/issues) for
any bugs, suggestions and feedback. Any additional feedback can be sent to
stevenbucher@microsoft.com.
