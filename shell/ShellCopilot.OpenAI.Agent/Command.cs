using System.CommandLine;
using System.CommandLine.Completions;
using ShellCopilot.Abstraction;

namespace ShellCopilot.OpenAI.Agent;

internal sealed class GPTCommand : CommandBase
{
    private readonly OpenAIAgent _agnet;

    public GPTCommand(OpenAIAgent agent)
        : base("gpt", "Command for GPT management within the 'openai-gpt' agent.")
    {
        _agnet = agent;

        var use = new Command("use", "Specify a GPT to use, or choose one from the available GPTs.");
        var useGPT = new Argument<string>(
            name: "GPT",
            getDefaultValue: () => null,
            description: "Name of a GPT.").AddCompletions(GPTNameCompleter);
        use.AddArgument(useGPT);
        use.SetHandler(UseGPTAction, useGPT);

        var list = new Command("list", "List a specific GPT, or all available GPTs.");
        var listGPT = new Argument<string>(
            name: "GPT",
            getDefaultValue: () => null,
            description: "Name of a GPT.").AddCompletions(GPTNameCompleter);
        list.AddArgument(listGPT);
        list.SetHandler(ListGPTAction, listGPT);

        AddCommand(list);
        AddCommand(use);
    }

    private void ListGPTAction(string name)
    {
        IHost host = Shell.Host;

        // Reload the setting file if needed.
        _agnet.ReloadSettings();

        Settings setting = _agnet.Settings;

        if (setting is null || setting.GPTs.Count is 0)
        {
            host.WriteErrorLine("No GPT instance defined.");
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            setting.ListAllGPTs(host);
            return;
        }

        try
        {
            _agnet.Settings.ShowOneGPT(host, name);
        }
        catch (InvalidOperationException ex)
        {
            string availableGPTNames = GPTNamesAsString();
            host.WriteErrorLine($"{ex.Message} Available GPT(s): {availableGPTNames}.");
        }
    }

    private void UseGPTAction(string name)
    {
        // Reload the setting file if needed.
        _agnet.ReloadSettings();

        var setting = _agnet.Settings;
        var host = Shell.Host;

        if (setting is null || setting.GPTs.Count is 0)
        {
            host.WriteErrorLine("No GPT instance defined.");
            return;
        }

        try
        {
            GPT chosenGPT = string.IsNullOrEmpty(name)
                ? host.PromptForSelectionAsync(
                    title: "[orange1]Please select a [Blue]GPT[/] to use[/]:",
                    choices: setting.GPTs,
                    converter: GPTName,
                    CancellationToken.None).GetAwaiter().GetResult()
                : setting.GetGPTByName(name);

            setting.UseGPT(chosenGPT);
            _agnet.UpdateDescription();
            host.MarkupLine($"Using the agent [green]{chosenGPT.Name}[/]:");
            host.WriteLine(_agnet.Description);
        }
        catch (InvalidOperationException ex)
        {
            string availableGPTNames = GPTNamesAsString();
            host.WriteErrorLine($"{ex.Message} Available GPT(s): {availableGPTNames}.");
        }
    }

    private static string GPTName(GPT gpt) => gpt.Name;
    private IEnumerable<string> GPTNameCompleter(CompletionContext context) => _agnet.Settings?.GPTs.Select(GPTName) ?? [];
    private string GPTNamesAsString() => string.Join(", ", GPTNameCompleter(null));
}
