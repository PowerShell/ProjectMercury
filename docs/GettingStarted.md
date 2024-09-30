# Getting Started with AIShell

AIShell was developed to help target key pain points of CLI users like finding the right
command/command combination to use, recover from errors and understand better commands and outputs
of those commands. Follow along and walk through some examples to get started with AIShell.

## What is AIShell

AIShell is a CLI executable that allows you to interact with different AI assistants within your
CLI. Each different AI assistant is known as an agent. You can choose which agent to use when you
start AIShell and switch between them when you are using AIShell.

In this version of AIShell, we are shipping two agents, one talking to an Azure OpenAI instance of
gpt-4o and an Azure agent that can help assist with Azure CLI commands. When this is formally
released you will need to provide your own Azure OpenAI deployment and configure the agent with its
specific details.

AIShell is an executable known as `aish.exe` and can be ran in your CLI for a full screen
experience. If you are utilizing Windows Terminal and PowerShell 7 you can use the `AIShell`
PowerShell module to create a split screen experience. This is the recommended way to use AIShell
because you will get key benefits of deeper integration into the shell. These features include:

- Inserting code from the sidecar response directly into the working shell
- Multi-step commands will be put into the predictive intellisense buffer for quick acceptance
- Easy, single command error recovery

## Starting AIShell

You can utilize the `Start-AIShell` command in the `AIShell` PowerShell module to open a split pane
experience in Windows Terminal. You will be prompted to choose an agent to use, and then you can
start chatting with the agent.

![A Gif showing Getting Started with AIShell](/docs/media/startAISHell.gif)

## Using AIShell

Now that you have the AIShell window open and selected an agent, you can start chatting with the
agent. The installed Azure OpenAI agent is grounded to be a PowerShell expert. This means we have
modified the system prompt to say it should act like a PowerShell expert, it is not trained on any
specific PowerShell code or documentation. The Azure agent is an agent designed to bring the Copilot
in Azure directly to your CLI and help provide Azure CLI and Azure PowerShell commands. Here are some sample queries you can ask to each agent:

### Azure OpenAI Agent

- "How do I create a text file named helloworld in PowerShell?"
- "What is the difference between a switch and a parameter in PowerShell?"
- How do I get the top 10 most CPU intensive processes on my computer?

### Azure Agent
- "How do I create a new resource group with Azure CLI?"
- "How can I list out the storage accounts I have in Azure PowerShell"
- "What is Application Insights?"
- "How to create a web app with Azure CLI?"

### Switching Agents

You can switch between agents by using the `@<agentName>` syntax in your chat messages. For example,

You can also use a chat command to switch agents. For example, to switch to the `openai-gpt` agent, use `/agent use openai-gpt`.

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

### Inserting code

When you are chatting with the agent, you can use the `/code post` command to automatically insert
the code from the response into the working shell. This is a great way to quickly get the code you
need to run in your shell. Additionally, you can use the hot key `Ctrl+d, Ctrl+d` to insert the code
into the working shell.

### Key bindings for commands

AIShell supports key bindings for the `/code` command. They are currently hard-coded, but custom key
bindings will be supported in future releases.

| Key bindings              | Command          | Functionality |
| ------------------------- | ---------------- | ------------- |
| <kbd>Ctrl+d, Ctrl+c</kbd> | `/code copy`     | Copy _all_ the generated code snippets to clipboard |
| <kbd>Ctrl+\<n\></kbd>     | `/code copy <n>` | Copy the _n-th_ generated code snippet to clipboard |
| <kbd>Ctrl+d, Ctrl+d</kbd> | `/code post`     | Post _all_ the generated code snippets to the connected application |
| <kbd>Ctrl+d, \<n\></kbd>  | `/code post <n>` | Post the _n-th_ generated code snippet to the connected application |

### Resolving Errors

If you encounter an error in your working terminal, you can use the `Resolve-Error` cmdlet to send
that error to the open AIShell window for resolution. This will allow you to get a response from the
AI model open to help you resolve the error.

![A Gif showing Resolving Errors with AIShell](/docs/media/ResolveError.gif)

### Invoking AIShell
Additionally for following up on commands and their output, you can use the `Invoke-AIShell` cmdlet
to send queries to the open AIShell window and current agent.

![A Gif showing Invoking AIShell with AIShell](/docs/media/InvokeAIShell.gif)



