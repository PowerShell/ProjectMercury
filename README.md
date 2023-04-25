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

Your API Key must be stored in `$env:AZURE_OPENAI_API_KEY`.
If you want to use a custom model endpoint, you can store it in `$env:AZURE_OPENAI_ENDPOINT`.

Additional opportunities:

- integration with debugger to get assistance
- identifying and fixing issues in a script file, enhance fixes with existing pester tests
- generation of Pester tests
- malware, unsafe security issues detection
