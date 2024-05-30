# Welcome to the AISH repository

**AISH** is our latest CLI tool that provides an interactive shell session to chat with large
language models. Users can use _agents_ to interact with different AI models, or other assistance
providers, in a conversational manner. **AISH** also provides a framework for creating AI agents.

This project is currently in an **alpha** state. Expect many significant changes to the code as we
experiment and refine the user experiences of this tool. We appreciate your feedback and patience as
we continue our development.

![GIF showing demo of AISH][04]

## Installing AISH

Some prerequisites for building AISH

- Build script requires PowerShell v7.2 or newer versions
- [PowerShell v7.4][11] is recommended
- [.NET SDK 8][09] is required to build the project

Here are the steps to install and use AISH.

1. Clone this repository, `git clone https://github.com/PowerShell/AISH`
2. Run `./build.ps1` in the repository's root directory to build the project
3. After the build is complete, you can find the produced executable `aish` in the `out\debug\app`
   folder within the repository's root directory. You can add the location to the `PATH` environment
   variable for easy access. The full path is copied to your clipboard after successful build.

## AI Agents

AISH provides a framework for creating and registering multiple AI Agents. The agents are libraries
that you use to interact with different AI models or assistance providers. Currently, theses are the
supported agents:

Agent README files:

- [`openai-gpt`][08]
- [`interpreter`][07]

When you run `aish`, you are prompted to choose an agent. For more details about each agent, see the
README in the each agent folder.

## How to use AISH

To start a chat session with the LLM, run `aish`, which starts a new session in your current window.
Choose the agent you would like to use. Once you select an agent you can begin your conversation.

We suggest using a split pane approach with the terminal of choice. In Windows Terminal, use the
following command to start `aish` in a new split pane:

```shell
wt -w 0 sp aish
```

You can bind this command to a key like `F3` in your PowerShell session. Add the following code to
your `$PROFILE` script:

```powershell
$PSReadLineSplat = @{
    Chord = 'F3'
    ScriptBlock = {
        wt -w 0 sp --tabColor '#345beb'--size 0.4 -p $env:WT_PROFILE_ID --title 'AISH' <full-path-to-aish.exe>
    }
}
Set-PSReadLineKeyHandler @PSReadLineSplat
```

### `/` commands

By default, `aish` provides a base set of chat `/` commands used to interact with the responses from
the AI model. To get a list of commands, use the `/help` command in the chat session.

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

Also, agents can implement their own commands. Some commands, such as `/like` and `/dislike`, are
commands that sends feedback to the agents. It is up to the agents to consume the feedback.

## Agent development

To learn more about how to create an agent for yourself please see, [Creating an Agent][03].

## Contributing to the project

Please see [CONTRIBUTING.md][02] for more details.

## Support

For support, see our [Support][05] statement.

## Code of Conduct

The project follows the Microsoft Open Source Code of Conduct. For more information, see the
[Code of Conduct FAQ][01].

## Feedback

We're still in development and value your feedback! Please file [issues][10] in this repository for
bugs, suggestions, or feedback.

<!-- link references -->
[01]: ./docs/CODE_OF_CONDUCT.md
[02]: ./docs/CONTRIBUTING.md
[03]: ./docs/development/CreatingAnAgent.md
[04]: ./docs/media/ShellCopilotDemo.gif
[05]: ./docs/SUPPORT.md
[07]: ./shell/ShellCopilot.Interpreter.Agent/README.md
[08]: ./shell/ShellCopilot.OpenAI.Agent/README.md
[09]: https://dotnet.microsoft.com/en-us/download
[10]: https://github.com/PowerShell/AISH/issues
[11]: https://learn.microsoft.com/powershell/scripting/install/installing-powershell
