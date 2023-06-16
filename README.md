# Microsoft.PowerShell.Copilot

```
██████╗ ███████╗ ██████╗ ██████╗ ██████╗ ██╗██╗      ██████╗ ████████╗
██╔══██╗██╔════╝██╔════╝██╔═══██╗██╔══██╗██║██║     ██╔═══██╗╚══██╔══╝
██████╔╝███████╗██║     ██║   ██║██████╔╝██║██║     ██║   ██║   ██║
██╔═══╝ ╚════██║██║     ██║   ██║██╔═══╝ ██║██║     ██║   ██║   ██║
██║     ███████║╚██████╗╚██████╔╝██║     ██║███████╗╚██████╔╝   ██║
╚═╝     ╚══════╝ ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝ ╚═════╝    ╚═╝
```

This module enable an interactive chat mode as well as getting the last error and sending to GPT.

Your User Key (Subscription Key) must be stored in `$env:API_SUB_KEY`.
If you want to use a custom model endpoint, you can store it in `$env:AZURE_OPENAI_ENDPOINT`.
A custom initial system prompt can be stored in `$env:AZURE_OPENAI_SYSTEM_PROMPT`.

Additional opportunities:

- integration with debugger to get assistance
- identifying and fixing issues in a script file, enhance fixes with existing pester tests
- generation of Pester tests
- malware, unsafe security issues detection
