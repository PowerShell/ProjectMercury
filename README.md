# AISH

This is a repository of various **AI** + **Sh**ell prototypes we have created to test out experiences and
features. **AISH** is the latest and most finished prototype. It is a CLI tool that creates
an interactive chat session with a registered Large Language Model. Currently we are in a **Private Preview** state and everything is subject to change.

![GIF showing demo of AISH](./docs/media/ShellCopilotDemo.gif)

## Installing and Using AISH

Some prerequisites for building AISH
- Build script requires PowerShell v7.2 or newer versions. [PowerShell v7.4](https://learn.microsoft.com/powershell/scripting/install/installing-powershell?view=powershell-7.4) is recommended.
- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download) is required to build the project.

Here are the steps to install and use AISH.
1. Clone this repository, `git clone https://github.com/PowerShell/ShellCopilot`;
2. Run `./build.ps1` in the repository's root directory to build the project;
3. After the build is complete, you can find the produced executable `aish` in the `out\debug` folder within the repository's root directory. You can add it to the `PATH` environment variable for easy access.

> Note: Depending on your OS directory paths may be `\` on Windows or `/` on Mac.

## Agent Concept

AISH has a concept of different AI Agents, these can be thought of like modules that users can use to interact with different AI models. Right now there are four supported agents.
- `az-cli`
- `az-ps`
- `openai-gpt`
- `interpreter`

If you run `aish` you will get prompted to choose between the two.

### Az-CLI Agent

This agent is for talking specifically to an Az CLI endpoint tailored to helping users with Azure CLI questions.

Prerequisites:
- Have [Azure CLI installed](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Login with an Azure account within the Microsoft tenant with `az login` command

### Az-PS Agent

This agent is for talking specifically to an Az PowerShell endpoint tailored to helping users with Azure PowerShell questions.

Prerequisites:
- Have [Azure PowerShell installed](https://learn.microsoft.com/powershell/azure/install-azure-powershell)
- Login with an Azure account within the Microsoft tenant with `Connect-AzAccount` command


### OpenAI-GPT Agent

This is a more generalized agent that users can bring their own instance of Azure OpenAI (or OpenAI) and a completely customizable system prompt.
Right now, it is defaulted to an internal Azure OpenAI endpoint with a prompt to be an assistant for PowerShell commands.

### Interpreter Agent

This agent utilizes the open interpreter that OpenAI endpoints provide, please see the [interpreter agent README](./shell/ShellCopilot.Interpreter.Agent/README.md) for more details.

## Getting an Azure OpenAI Endpoint

If you have separate Azure OpenAI endpoint you can use that instead of the one above. Read more at
[Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/azure/ai-services/openai/how-to/create-resource?pivots=ps).

## Using AISH

To start a chat session with the LLM, simply run `aish` and it will open up a new session in your current window.
We suggest using a split pane approach with the terminal of choice.
Windows Terminal offers an easy pane option by running:

```shell
wt -w 0 sp aish
```

If you use Windows Terminal and would like to tie this command to a key like `F3` in your PowerShell session,
you can add the following code to your `$PROFILE`:

```powershell
Set-PSReadLineKeyHandler -Chord F3 -ScriptBlock { wt -w 0 sp --tabColor '#345beb'--size 0.4 -p "<your-default-WT-profile-guid>" --title 'AISH' <full-path-to-aish.exe> }
```

## Contributing

Please see [CONTRIBUTING.md](./docs/CONTRIBUTING.md) for more details.

## Support

For support, see our [Support Section](./docs/SUPPORT.md).

## Code of Conduct

For more information, see the [Code of Conduct FAQ](./docs/CODE_OF_CONDUCT.md) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Feedback

We still in development and value any and all feedback! Please file an [issue in this repository](https://github.com/PowerShell/ShellCopilot/issues) for
any bugs, suggestions and feedback. Any additional feedback can be sent to
stevenbucher@microsoft.com.
