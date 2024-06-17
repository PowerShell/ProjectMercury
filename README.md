# Welcome to Project Mercury

**Project Mercury** contains our latest CLI tool that provides an interactive shell session to chat
with language models, creating an *AI Shell*. Users can use _agents_ to interact with different AI models, or other
assistance providers, in a conversational manner. **Project Mercury** also provides a framework for
creating AI agents.

Why the name **Project Mercury**? The name is inspired both by the Roman god of messages and the
first human spaceflight by the US. This project is our first step into the new world of AI powered
assistance and focuses on being the connection (or messenger) between the user and the AI model.

This project is currently in the **alpha** phase. Expect many significant changes to the code as we
experiment and refine the user experiences of this tool. We appreciate your feedback and patience as
we continue our development.

![GIF showing demo of the AI Shell][04]

## Installation

Some prerequisites for building an AI Shell:

- Build script requires PowerShell v7.2 or newer versions
- [PowerShell v7.4][11] is recommended
- [.NET SDK 8][09] is required to build the project

Here are the steps to install and use.

1. Clone this repository, `git clone https://github.com/PowerShell/ProjectMercury`
2. Run `./build.ps1` in the repository's root directory to build the project
3. After the build is complete, you can find the produced executable `aish` in the `out\debug\app`
   folder within the repository's root directory. You can add the location to the `PATH` environment
   variable for easy access. The full path is copied to your clipboard after successful build.

## AI Agents

Project Mercury provides a framework for creating and registering multiple AI Agents. The agents are
libraries that you use to interact with different AI models or assistance providers. Currently,
these are the supported agents:

Agent README files:

- [`az-cli` & `az-ps`][13]
- [`openai-gpt`][08]
- [`ollama`][06]
- [`interpreter`][07]

When you run `aish`, you are prompted to choose an agent. For more details about each agent, see the
README in the each agent folder.

To learn more about how to create an agent for yourself please see, [Creating an Agent][03].

## Usage

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
        wt -w 0 sp --tabColor '#345beb'--size 0.4 -p $env:WT_PROFILE_ID --title 'AIShell' <full-path-to-aish.exe>
    }
}
Set-PSReadLineKeyHandler @PSReadLineSplat
```

Similarly, you can use iTerm2 to get a similiar split pane experience on MacOS. You can split the pane vertically by pressing `Cmd + D` and then run `aish` in one of the panes.

### Chat commands

By default, `aish` provides a base set of chat `/` commands used to interact with the responses from
the AI model. To get a list of commands, use the `/help` command in the chat session.

```
  Name       Description                                      Source
──────────────────────────────────────────────────────────────────────
  /agent     Command for agent management.                    Core
  /cls       Clear the screen.                                Core
  /code      Command to interact with the code generated.     Core
  /dislike   Dislike the last response and send feedback.     Core
  /exit      Exit the interactive session.                    Core
  /help      Show all available commands.                     Core
  /like      Like the last response and send feedback.        Core
  /refresh   Refresh the chat session.                        Core
  /render    Render a markdown file, for diagnosis purpose.   Core
  /retry     Regenerate a new response for the last query.    Core
```

Also, agents can implement their own commands. For example, the `openai-gpt` agent register the command `/gpt`
for managing the GPTs defined for the agent. Some commands, such as `/like` and `/dislike`, are commands that
sends feedback to the agents. It is up to the agents to consume the feedback.

### Key bindings for commands

AI Shell supports key bindings for the `/code` command.
They are currently hard-coded, but custom key bindings will be supported in future releases.

| Key bindings              | Command          | Functionality |
| ------------------------- | ---------------- | ------------- |
| <kbd>Ctrl+d, Ctrl+c</kbd> | `/code copy`     | Copy _all_ the generated code snippets to clipboard |
| <kbd>Ctrl+\<n\></kbd>     | `/code copy <n>` | Copy the _n-th_ generated code snippet to clipboard |
| <kbd>Ctrl+d, Ctrl+d</kbd> | `/code post`     | Post _all_ the generated code snippets to the connected application |
| <kbd>Ctrl+d, \<n\></kbd>  | `/code post <n>` | Post the _n-th_ generated code snippet to the connected application |

### Configuration

Currently, AI Shell supports very basic configuration. One can creates a file named `config.json` under `~/.aish` to configure AI Shell,
but it only supports declaring the default agent to use at startup. This way you do not need to select agents everytime you run `aish.exe`

Configuration of AI Shell will be improved in future releases to support custom key bindings, color themes and more.

```json
{
  "DefaultAgent": "openai-gpt"
}
```

## Contributing to the project

Please see [CONTRIBUTING.md][02] for more details.

## Support

For support, see our [Support][05] statement.

## Code of Conduct

Please see our [Code of Conduct][01] before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy][12].

## Feedback

We're still in development and value your feedback! Please file [issues][10] in this repository for
bugs, suggestions, or feedback.

<!-- link references -->
[01]: ./docs/CODE_OF_CONDUCT.md
[02]: ./docs/CONTRIBUTING.md
[03]: ./docs/development/CreatingAnAgent.md
[04]: ./docs/media/AIShellDemo.gif
[05]: ./docs/SUPPORT.md
[06]: ./shell/agents/AIShell.Ollama.Agent/README.md
[07]: ./shell/agents/AIShell.Interpreter.Agent/README.md
[08]: ./shell/agents/AIShell.OpenAI.Agent/README.md
[09]: https://dotnet.microsoft.com/en-us/download
[10]: https://github.com/PowerShell/ProjectMercury/issues
[11]: https://learn.microsoft.com/powershell/scripting/install/installing-powershell
[12]: ./docs/SECURITY.md
[13]: ./shell/agents/AIShell.Azure.Agent/README.md
