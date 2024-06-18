# Shell Integration Module

The `AIShell` module is a PowerShell module that creates a connection between PowerShell 7 and the
AIShell launched in a side car of your terminal. The module provide deep integration with the
interactive features of PowerShell 7, such as Predictive IntelliSense. It also enables the cross
pane communication between the AIShell agent and the PowerShell that sends queries, errors, and
results between the two shells. The following images shows the current capabilities:

![Shell Integration Module](../../docs/media/ShellIntegrationDemo.gif)

## Installation and Usage

This module gets built when you run the `build.ps1` script in the root of the repository. The build
script writes the module to the `out/debug/module` directory. To import the module, run the
following command:

```powershell
Import-Module .\out\debug\module\AIShell
```

The module contains the following cmdlets with their respective aliases:

- `Start-AIShell` - alias `aish`
- `Resolve-Error` - alias `fixit`
- `Invoke-AIShell` - alias `askai`

### Start-AIShell

This cmdlet starts an AIShell session in a split pane window of Windows Terminal or iTerm2 with a
connected communication channel to the PowerShell session that started it. This is necessary to do
to get any of the shell integration features highlighted in the demo above.

### Resolve-Error

When you encounter and error in your working shell and are unsure what to do, instead of copying and
pasting the error message to the AIShell agent, you can run this cmdlet to send the error to the
agent for resolution. This sends the entire error object to the agent for analysis and resolution.
The `Start-AIShell` cmdlet must be run before this cmdlet can be used.

### Invoke-AIShell

This cmdlet allows you to send a query to the AIShell agent to execute. This is useful in case you
do not want to switch between the two panes but want to send a query to the agent. The
`Start-AIShell` cmdlet must be run before this cmdlet can be used.

### /code post

One of the built in chat commands we have in AI Shell is the `/code` command. This command allows
you to interact with the code suggested by the assistance provider in an agent. There is a
subcommand for this command called `post` that copies the code to the working shell and
Predictive IntelliSense buffer of your working shell. Meaning you can run the code in your working
shell and get Predictive IntelliSense for the steps provided by the agent.

![Predictive IntelliSense Demo](../../docs/media/AIShellPredictiveIntelliSenseDemo.gif)
