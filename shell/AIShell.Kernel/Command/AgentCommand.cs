using System.CommandLine;
using System.CommandLine.Completions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

internal sealed class AgentCommand : CommandBase
{
    public AgentCommand()
        : base("agent", "Command for agent management.")
    {
        var use = new Command("use", "Specify an agent to use, or choose one from the available agents.");
        var useAgent = new Argument<string>(
            name: "agent",
            getDefaultValue: () => null,
            description: "Name of an agent.").AddCompletions(AgentCompleter);
        use.AddArgument(useAgent);
        use.SetHandler(UseAgentAction, useAgent);

        var config = new Command("config", "Open up the setting file for an agent. When no agent is specified, target the active agent.");
        var editor = new Option<string>("--editor", "The editor to open the setting file in.");
        var configAgent = new Argument<string>(
            name: "agent",
            getDefaultValue: () => null,
            description: "Name of an agent.").AddCompletions(AgentCompleter);
        config.AddArgument(configAgent);
        config.AddOption(editor);
        config.SetHandler(ConfigAgentAction, configAgent, editor);

        var list = new Command("list", "List all available agents.");
        list.SetHandler(ListAgentAction);

        AddCommand(config);
        AddCommand(list);
        AddCommand(use);
    }

    private void ListAgentAction()
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        var active = shell.ActiveAgent;
        var list = shell.Agents;

        var elements = new IRenderElement<LLMAgent>[]
        {
            new CustomElement<LLMAgent>("Name", c => c == active ? $"{c.Impl.Name} (active)" : c.Impl.Name),
            new CustomElement<LLMAgent>("Description", c => c.Impl.Description),
        };

        host.RenderTable(list, elements);
    }

    private void UseAgentAction(string name)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        LLMAgent chosenAgent = string.IsNullOrEmpty(name)
            ? host.PromptForSelectionAsync(
                title: "[orange1]Please select an [Blue]agent[/] to use[/]:",
                choices: shell.Agents,
                converter: AgentName).GetAwaiter().GetResult()
            : FindAgent(name, shell);

        if (chosenAgent is null)
        {
            AgentNotFound(name, shell);
            return;
        }

        shell.SwitchActiveAgent(chosenAgent);
        host.MarkupLine($"Using the agent [green]{chosenAgent.Impl.Name}[/]:");
        chosenAgent.Display(host);
    }

    private void ConfigAgentAction(string name, string editor)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        LLMAgent chosenAgent = string.IsNullOrEmpty(name)
            ? shell.ActiveAgent
            : FindAgent(name, shell);

        if (chosenAgent is null)
        {
            AgentNotFound(name, shell);
            return;
        }

        var current = chosenAgent.Impl;
        var settingFile = current.SettingFile;
        if (settingFile is null)
        {
            host.WriteErrorLine($"The agent '{current.Name}' doesn't support configuration.");
            return;
        }

        try
        {
            ProcessStartInfo info;
            if (!string.IsNullOrEmpty(editor))
            {
                info = new(editor)
                {
                    ArgumentList = { settingFile },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process.Start(info);
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                string ext = Path.GetExtension(settingFile);
                if (!Interop.HasDefaultApp(ext, out string defaultApp))
                {
                    defaultApp = "notepad.exe";
                }

                // Handle VSCode specially because when simply using shell execute to start VSCode from a console app,
                // it writes log messages to the cosnole output and there is no way to suppress it.
                // However, it is very common for users to set VSCode as the default editor for many file extensions,
                // so we want to make it work as expected.
                // It turns out shell execute uses "...\Microsoft VS Code\Code.exe", but instead, we should use the CLI
                // version "...\Microsoft VS Code\bin\code.cmd" to avoid the log messages.
                if (defaultApp.EndsWith(@"Microsoft VS Code\Code.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string code = Path.Combine(Path.GetDirectoryName(defaultApp), @"bin\code.cmd");
                    if (Path.Exists(code))
                    {
                        defaultApp = code;
                    }
                }

                info = defaultApp.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                    ? new("cmd.exe") { ArgumentList = { "/c", defaultApp, settingFile } }
                    : new(defaultApp) { ArgumentList = { settingFile } };

                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                Process.Start(info);
            }
            else
            {
                // On macOS and Linux, we just depend on the default editor.
                info = new(settingFile) { UseShellExecute = true };
                Process.Start(info);
            }
        }
        catch (Exception ex)
        {
            host.WriteErrorLine(ex.Message);
        }
    }

    private IEnumerable<string> AgentCompleter(CompletionContext context)
    {
        var shell = (Shell)Shell;
        return shell.Agents.Select(AgentName);
    }

    private static bool HasAnyAgent(Shell shell, Host host)
    {
        if (shell.Agents.Count is 0)
        {
            host.WriteErrorLine("No agent is available.");
            return false;
        }

        return true;
    }

    private static string AgentName(LLMAgent agent)
    {
        return agent.Impl.Name;
    }

    internal static LLMAgent FindAgent(string name, Shell shell)
    {
        return shell.Agents.FirstOrDefault(a => string.Equals(name, a.Impl.Name, StringComparison.OrdinalIgnoreCase));
    }

    internal static void AgentNotFound(string name, Shell shell)
    {
        string availableAgentNames = string.Join(", ", shell.Agents.Select(AgentName));
        shell.Host.WriteErrorLine($"Cannot find an agent with the name '{name}'. Available agent(s): {availableAgentNames}.");
    }
}

internal static partial class Interop
{
    [Flags]
    public enum AssocF
    {
        None = 0,
        Init_NoRemapCLSID = 0x1,
        Init_ByExeName = 0x2,
        Open_ByExeName = 0x2,
        Init_DefaultToStar = 0x4,
        Init_DefaultToFolder = 0x8,
        NoUserSettings = 0x10,
        NoTruncate = 0x20,
        Verify = 0x40,
        RemapRunDll = 0x80,
        NoFixUps = 0x100,
        IgnoreBaseClass = 0x200,
        Init_IgnoreUnknown = 0x400,
        Init_Fixed_ProgId = 0x800,
        Is_Protocol = 0x1000,
        Init_For_File = 0x2000
    }

    public enum AssocStr
    {
        Command = 1,
        Executable,
        FriendlyDocName,
        FriendlyAppName,
        NoOpen,
        ShellNewValue,
        DDECommand,
        DDEIfExec,
        DDEApplication,
        DDETopic,
        InfoTip,
        QuickTip,
        TileInfo,
        ContentType,
        DefaultIcon,
        ShellExtension,
        DropTarget,
        DelegateExecute,
        Supported_Uri_Protocols,
        ProgID,
        AppID,
        AppPublisher,
        AppIconReference,
        Max
    }

    [LibraryImport("Shlwapi.dll", EntryPoint = "AssocQueryStringW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint AssocQueryString(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string pszExtra,
        [Out] char[] pszOut,
        ref int pcchOut);

    /// <summary>
    /// The method returns "C:\WINDOWS\system32\OpenWith.exe" when the file extension
    /// is not associated with a default application.
    /// </summary>
    internal static bool HasDefaultApp(string extension, out string executable)
    {
        const int S_OK = 0;
        const int S_FALSE = 1;

        int length = 0;
        executable = null;

        uint ret = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref length);
        if (ret != S_FALSE)
        {
            return false;
        }

        char[] charArray = new char[length];
        ret = AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, charArray, ref length);
        if (ret != S_OK)
        {
            return false;
        }

        executable = new string(charArray, 0, length - 1);
        return true;
    }
}
