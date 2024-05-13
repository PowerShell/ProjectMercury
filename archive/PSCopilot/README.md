# PowerShell Copilot 

**PowerShell Copilot** was the one of the first prototype we created. It is a PowerShell module that
allows you to also have an interactive chat session with an LLM. It has slightly different UX/UI
than AISH and is specific to PowerShell.

To use PSCopilot, 
1. Run `./build.ps1 -PSCopilot` to build the module
2. Import the module via `Import-Module ./out/Microsoft.PowerShell.Copilot`
3. Set `$env:AZURE_OPENAI_API_KEY = <YOUR KEY>` to the key you got from the previous steps in [AISH README](../../README.md). 
4. Add steps 2 and 3 to your `$PROFILE` to be able to use it anytime you open up PowerShell.
5. Run `Enable-PSCopilotKeyHandler` to set `F3` to the key to enter and exit the session
6. Press `F3` and start chatting!

You can use `Ctrl+C` to copy any code from the LLMs to your clipboard. You can also set additional
prompt context to `$env:AZURE_OPENAI_SYSTEM_PROMPT` to better ground the LLM.
