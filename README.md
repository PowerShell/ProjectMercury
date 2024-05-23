# AISH

Welcome to the AISH repository! **AISH** is our latest CLI tool that creates an interactive chat
shell session with large language models. It is designed to be a platform for creating AI agents
that can interact with users in a conversational manner where users can build or use *agents* to
interact with different AI models or forms of assistance.

Please be aware that this repository and its associated product are currently in an **alpha** state.
There will likely be many experimental and significant changes to the code base and experience over
time. We appreciate your feedback and patience as we continue to develop and refine our offering.

![GIF showing demo of AISH](./docs/media/ShellCopilotDemo.gif)

## Installing AISH

Some prerequisites for building AISH
- Build script requires PowerShell v7.2 or newer versions. [PowerShell v7.4](https://learn.microsoft.com/powershell/scripting/install/installing-powershell?view=powershell-7.4) is recommended.
- [.NET SDK 8](https://dotnet.microsoft.com/en-us/download) is required to build the project.

Here are the steps to install and use AISH.

1. Clone this repository, `git clone https://github.com/PowerShell/AISH`;
2. Run `./build.ps1` in the repository's root directory to build the project;
3. After the build is complete, you can find the produced executable `aish` in the `out\debug`
   folder within the repository's root directory. You can add it to the `PATH` environment variable
   for easy access. The full path will be copied to your clipboard after successful build.

> Note: Depending on your OS directory paths may be `\` on Windows or `/` on Mac.

## Agent Concept

AISH has a concept of different AI Agents, these can be thought of like modules that users can use
to interact with different AI models or forms of assistance. Right now there are four supported
agents in this repo.
- [`az-cli`](./shell/ShellCopilot.Azure.Agent/README.md)
- [`az-ps`](./shell/ShellCopilot.Azure.Agent/README.md)
- [`openai-gpt`](./shell/ShellCopilot.OpenAI.Agent/README.md)
- [`interpreter`](./shell/ShellCopilot.Interpreter.Agent/README.md)

If you run `aish` you will get prompted to choose between these agents. Please refer to the READMEs
in the respective agent folders for details.

## Using AISH

To start a chat session with the LLM, simply run `aish` and it will open up a new session in your
current window. You will get prompted for which agent you would like to use. Once you select an
agent you can begin your conversation.

We suggest using a split pane approach with the terminal of choice. Windows Terminal
offers an easy pane option by running:

```shell
wt -w 0 sp aish
```

If you use Windows Terminal and would like to tie this command to a key like `F3` in your PowerShell
session, you can add the following code to your `$PROFILE`:

```powershell
Set-PSReadLineKeyHandler -Chord F3 -ScriptBlock { wt -w 0 sp --tabColor '#345beb'--size 0.4 -p "<your-default-WT-profile-guid>" --title 'AISH' <full-path-to-aish.exe> }
```

### `/` commands

The base AISH offers a number of chat `/` commands that can be used to interact with the responses
from the AI model. You can find all the available chats by running `/help` in the chat session.

```
  Name       Description
────────────────────────────────────────────────────────────
  /agent     Command for agent management.
  /cls       Clear the screen.
  /code      Command to interact with the code generated.
  /dislike   Dislike the last response and send feedback.
  /exit      Exit the interactive session.
  /help      Show all available commands.
  /like      Like the last response and send feedback.
  /refresh   Refresh the chat session.
  /retry     Regenerate a new response for the last query.
```

Agents may implement their own commands so be sure to check `/help` while using an agent. Some
commands like `/like` and `/dislike` are just bare bones commands and need to be implemented by the
agent to be useful.

## Development

To learn more about how to create an agent for yourself please see the
[Creating an Agent Docs](./docs/development/CreatingAnAgent.md).

## Contributing

Please see [CONTRIBUTING.md](./docs/CONTRIBUTING.md) for more details.

## Support

For support, see our [Support Section](./docs/SUPPORT.md).

## Code of Conduct

For more information, see the [Code of Conduct FAQ](./docs/CODE_OF_CONDUCT.md) or contact
[opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Feedback

We still in development and value any and all feedback! Please file an
[issue in this repository](https://github.com/PowerShell/AISH/issues) for any bugs,
suggestions and feedback. 
