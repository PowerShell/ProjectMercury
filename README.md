# ![logo][]AI Shell (a.k.a Project Mercury)

Welcome to the **AI Shell** repository! AI Shell is a CLI tool that brings the power of artificial
intelligence directly to your command line! Designed to help you get command assistance from various
AI assistants, AI Shell is a versatile tool to help you become more productive in the command line.
We call these various AI assistant providers _agents_. You can use agents to interact with different
generative AI models or other AI/ML/assistant providers in a conversational manner. This repo
contains the code of the AI Shell engine, agents and details on how to create your own agent.

Why the name **Project Mercury**? This was the original code name of the project and was inspired
both by the Roman god of messages and the first human spaceflight by the US. This project is our
first step into the new world of AI powered assistance and focuses on being the connection (or
messenger) between the user and the AI model.

This project is currently in a very early **public preview** state. Expect many significant changes
to the code as we experiment and refine the user experiences of this tool. We appreciate your
feedback and patience as we continue our development.

![GIF showing demo of the AI Shell][04]

## New to AI Shell?

To learn more about AI Shell, we recommend you check out the [overview][19] page of the AI Shell
documentation.

There are two modes to use AI Shell, standalone and a side-by-side, integrated experience with
PowerShell 7. For more information see,
- [Get Started with AI Shell in PowerShell][15]
- [Get Started with AI Shell (standalone)][16]

## Getting AI Shell

AI Shell is supported on Windows, MacOS and Linux, however the best experience you can have is with
Windows, [PowerShell 7][11] and [Windows Terminal][14]. For more information see,
[Installing AI Shell][13].

## Locally Building AI Shell

Some prerequisites for building an AI Shell:

- Build script requires [PowerShell v7.4][18] or newer versions
- [.NET SDK 8][09] is required to build the project

Here are the steps to install and use.

1. Clone this repository, `git clone https://github.com/PowerShell/ProjectMercury`
2. Import the `build.psm1` module by running `import-module ./build.psm1` 
3. Run the `Start-Build` command (You can specify which agents build with the `-AgentsToInclude`
   parameter)
4. After the build is complete, you can find the produced executable `aish` in the `out\debug\app`
   folder within the repository's root directory. You can add the location to the `PATH` environment
   variable for easy access. The full path is copied to your clipboard after successful build.

## AI Agents

Project Mercury provides a framework for creating and registering multiple AI Agents. The agents are
libraries that you use to interact with different AI models or assistance providers. AI Shell
releases with two agents, the `openai-gpt` and `azure` agent. However there are additional ones
supported if you locally build the project: 

Agent README files:

- [`openai-gpt`][08] (shipped with AI Shell)
- [`ollama`][06]
- [`interpreter`][07]
- [`azure`][17] (shipped with AI Shell)

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

## Privacy

AI Shell does not capture, collect, store, or process any personal data or personally identifiable information (PII). All data interactions 
are limited to the scope of the functionality provided by the tool and do not involve any form of personal data collection.

Some agents integrated with AI Shell may collect telemetry data to improve performance, enhance user experience, or troubleshoot issues. 
We recommend that you refer to the individual agent’s README or documentation for more information on the telemetry practices and data collection 
policies for each agent.

If you are interested in learning more, see [Microsoft's Privacy Statement](https://www.microsoft.com/en-us/privacy/privacystatement?msockid=1fe60b30e66967f13fb91f29e73f661a).

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
[08]: https://learn.microsoft.com/powershell/utility-modules/aishell/how-to/agent-openai
[09]: https://dotnet.microsoft.com/en-us/download
[10]: https://github.com/PowerShell/ProjectMercury/issues
[11]: https://learn.microsoft.com/powershell/scripting/install/installing-powershell
[12]: ./docs/SECURITY.md
[13]: https://learn.microsoft.com/powershell/utility-modules/aishell/install-aishell
[14]: https://learn.microsoft.com/windows/terminal/
[15]: https://learn.microsoft.com/powershell/utility-modules/aishell/get-started/aishell-powershell
[16]:https://learn.microsoft.com/powershell/utility-modules/aishell/get-started/aishell-standalone
[17]: https://learn.microsoft.com/powershell/utility-modules/aishell/how-to/agent-azure
[18]: https://github.com/PowerShell/PowerShell/releases/tag/v7.4.6
[19]: https://learn.microsoft.com/powershell/utility-modules/aishell/overview
[logo]: ./docs/media/AIShellIconSVG.svg