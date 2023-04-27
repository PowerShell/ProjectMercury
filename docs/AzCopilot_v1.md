# Design intent for v1 of AzCopilot

As we apply the principle "Azure first, but not Azure only" and similarly "PowerShell first, but not PowerShell only",
we want to enable use of Azure OpenAI across different shells primarily focused on an enhanced experience for PowerShell
and Bash for CloudShell.

## Goals

- Drive adoption of Azure OpenAI via a console experience
- Shell agnostic
- Extensibility for enhanced experience for specific shell integration
- Team developed shell integration for PowerShell
- Enable partner and enterprise extensibility for models

## Priorities

Private Preview:

- Use or prompt to create Azure OpenAI instance under user's subscription
- No shell integration
- Interactive chat session

Public Preview:

- PowerShell integration
- Model registration
- Telemetry

## Azure OpenAI

User must authenticate to Azure and if they don't already have an Azure OpenAI instance tagged
for AzCopilot, they will be prompted to create one.

## CloudShell

One of the initial deployments of this should be in CloudShell enabled by default.
The creation and registration of Azure OpenAI should work in this environment.

## Supporting multiple shells

To support different shells, the core user experience would need to be a native executable (likely written in Rust)
with extensibility to allow shell integration for passing data in and out of the chat session.

For PowerShell, this would be wrapper cmdlets to provide a first class experience and for other shells,
it could be sub-commands to the native executable.

### Native executable

For interactive use, it is preferable to have a short executable name.
Can we simply use `ai`?

The executable would have the following sub-commands:

- `ai chat` - Start an interactive chat session using alternate screen buffer
- `ai init` - Initialize the Azure OpenAI instance.  May have additional subcommands for retrieiving user API key.
- `ai model` - A way to switch between models: AzPowerShell, general GPT35, enterprise specific instance, etc...
  - `ai model list`, `ai model register`, etc... to manage models
- `ai send` - Send content to a chat session.  Could be via STDIN or a path to a file.

To ensure the chat session has previous context, need either a way to have a long running process or caching of the context.
Initially, it would be simpler to use caching of the context and should be per shell session.
In the future, we may want a long running process so that the delay in getting a response allows the user to
switch back to the shell and continue working.

To have a hotkey switch between the chat session and the shell, this would require shell integration.
For PowerShell, we can register a PSReadLineKeyHandler.
For Bash, as a function key is a VT escape sequence, we can use `bind` to register a function to handle the key.

### PowerShell

Wrapper cmdlets to provide a more integrated experience:

- `Enter-AzCopilotChat` - Start an interactive chat session using alternate screen buffer
- `Initialize-AzCopilot` - Initialize the Azure OpenAI instance.  May have additional subcommands for retrieiving user API key.
- `Set-AzCopilotModel` - A way to switch between models: AzPowerShell, general GPT35, enterprise specific instance, etc...
  - `Get-AzCopilotModel`, `Register-AzCopilotModel`, etc... to manage models
- `Send-AzCopilot` - Send content to a chat session with optional `-LastError` switch to use the last error as context.

Additional shell integration may include the abilty for a cmdlet to include model name in their ErrorRecord
so that error analysis automatically uses that model.

## User Experience

Like the PSCopilot proof-of-concept, the chat session would be in the alternate screen buffer.
A different colored background (dark purple default), would be used to indicate the chat session vs the regular shell session.
As this chat session is itself a mini-shell, it would need a complete readline experience including:

- History
- Redrawing the screen buffer when re-entering the chat
- Built-in commands
  - `exit` - Exit the chat session
  - `help` - Display help
  - `model` - Switch between models: `model list`, `model set`
  - `clear` - Clear the screen
  - `reset` - Reset the chat session (clear history, context, etc...)
  - `copy` - Copy the last code snippet to the clipboard

## API Keys

As this is a shell agnostic solution, we cannot rely on SecretManagement to store the API keys.
The simplest option initially would be to use the SSH client model with the keys stored as files in a `.azcopilot` folder
using the model name as the file name with a `.key` extension.

Longer term, it would make sense to have a general native SecretManagement executable that can be used to store and retrieve
secrets for different shells.

## Support multiple models

Users may want to leverage different models tuned for specific scenarios: Azure PowerShell, Python model, enterprise specific, etc...

The information needed for a model would include:

- The model name
- The model endpoint URL
- The model system prompt (optional)
- The model description (optional)
- Max tokens for context and response (optional)
- Whether the model is trusted to send data to (default to false)

Regisration would be via a JSON file.
For development or ad-hoc testing, it may make sense to allow these to be passed via command line parameters,
but this is not a priority for v1.

Model registration would be stored as a JSON file in the user's `.azcopilot` folder with the model name as the file name
with a `.model` extension.

## Configuration

Configuration would be stored in a JSON file in the user's `.azcopilot` folder.

The configuration would include:

- The default model to use
- Shell integration registration (TBD)

## Telemetry

There will be both client side and server side telemetry.

### Client side telemetry

Client side telemetry would use ApplicationInsights and woudl be opt-out.

On the client side, we would like to collect:

- Number of models registered
- Features being used:
  - Chat
  - Error analysis
  - Test generation
  - Security analysis
  - Performance analysis
  - etc...

### Server side telemetry

The client can send a unique `User-Agent` header that identifies the node and/or session that is sending the request.
We would need to talk to the Azure OpenAI team to see if they can provide us with a way to correlate the requests
so that we know our client is driving their usage.
