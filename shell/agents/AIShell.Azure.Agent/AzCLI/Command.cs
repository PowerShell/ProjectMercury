using System.CommandLine;
using System.Text;
using System.Text.Json;
using AIShell.Abstraction;

namespace AIShell.Azure.CLI;

internal sealed class ReplaceCommand : CommandBase
{
    private readonly AzCLIAgent _agent;
    private readonly Dictionary<string, string> _values;

    public ReplaceCommand(AzCLIAgent agent)
        : base("replace", "Replace argument placeholders in the generated scripts with the real value.")
    {
        _agent = agent;
        _values = [];
        this.SetHandler(ReplaceAction);
    }

    private void ReplaceAction()
    {
        _values.Clear();

        IHost host = Shell.Host;
        var pInfo = _agent.PlaceholderInfo;

        if (pInfo is null)
        {
            host.WriteErrorLine("No argument placeholder to replace.");
            return;
        }

        List<PlaceholderItem> items = pInfo.Placeholders;
        string subText = items.Count > 1
            ? $"all {items.Count} argument placeholders"
            : "the argument placeholder";
        host.WriteLine($"\nWe'll provide assistance in replacing {subText} and regenerating the result. You can press 'Ctrl+c' to exit the assistance.\n");
        host.RenderDivider("Input Values");
        host.WriteLine();

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var dataType = Enum.Parse<ArgumentInfo.DataType>(item.Type, ignoreCase: true);

                ArgumentInfo argInfo;
                if (item.ValidValues?.Count > 0)
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, mustChooseFromSuggestions: true, item.ValidValues);
                }
                else if (item.Name.Contains("resourceGroup", StringComparison.OrdinalIgnoreCase))
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, mustChooseFromSuggestions: false, ["testVM-rg", "function-rg", "api-endpoint-rg"]);
                }
                else if (item.Name.Contains("adminUser", StringComparison.OrdinalIgnoreCase))
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, mustChooseFromSuggestions: false, ["localadmin", "john-test"]);
                }
                else if (item.Name.Contains("image", StringComparison.OrdinalIgnoreCase))
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, mustChooseFromSuggestions: false, ["UbuntuLTS", "Win10", "Win11", "RedHat"]);
                }
                else if (item.Name.Contains("vmSize", StringComparison.OrdinalIgnoreCase))
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, mustChooseFromSuggestions: true, ["A0", "A1", "A2", "A3", "A4"]);
                }
                else
                {
                    argInfo = new ArgumentInfo(item.Name, item.Desc, dataType);
                }

                host.Write($"{i+1}. ");
                string value = host.PromptForArgument(argInfo, Shell.CancellationToken);

                if (!string.IsNullOrEmpty(value))
                {
                    _values.Add(item.Name, value);
                }

                host.WriteLine();
            }
        }
        catch (OperationCanceledException)
        {
            bool proceed = false;
            if (_values.Count > 0)
            {
                host.WriteLine();
                proceed = host.PromptForConfirmationAsync(
                    "Would you like to regenerate with the provided values so far?",
                    defaultValue: false,
                    CancellationToken.None).GetAwaiter().GetResult();
                host.WriteLine();
            }

            if (!proceed)
            {
                return;
            }
        }

        if (_values.Count > 0)
        {
            host.RenderDivider("Summary");
            host.WriteLine("\nThe following placeholders will be replace:");
            host.RenderList(_values);

            host.RenderDivider("Regenerate");
            host.MarkupLine($"\nQuery: [teal]{pInfo.Query}[/]");

            string answer = host.RunWithSpinnerAsync(Regenerate).GetAwaiter().GetResult();
            host.RenderFullResponse(answer);
        }
        else
        {
            host.WriteLine("No value was specified for any of the argument placeholders.");
        }
    }

    private async Task<string> Regenerate()
    {
        PlaceholderInfo pInfo = _agent.PlaceholderInfo;
        StringBuilder prompt = new(capacity: pInfo.Query.Length + _values.Count * 15);
        prompt.Append("Regenerate for the last query using the following values specified for the argument placeholders.\n\n");

        foreach (var entry in _values)
        {
            prompt.Append($"{entry.Key}: {entry.Value}\n");
        }

        ResponseData data = pInfo.Response;
        foreach (CommandItem command in data.CommandSet)
        {
            foreach (var entry in _values)
            {
                command.Script = command.Script.Replace(entry.Key, entry.Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        List<PlaceholderItem> placeholders = pInfo.Placeholders;
        if (placeholders.Count > _values.Count)
        {
            List<PlaceholderItem> newList = new(placeholders.Count - _values.Count);
            foreach (PlaceholderItem item in placeholders)
            {
                if (!_values.ContainsKey(item.Name))
                {
                    newList.Add(item);
                }
            }

            data.PlaceholderSet = newList;
        }

        _agent.AddMessageToHistory(prompt.ToString(), fromUser: true);
        _agent.AddMessageToHistory(JsonSerializer.Serialize(data, Utils.JsonOptions), fromUser: false);

        // We are doing the replacement locally, but want to fake the regeneration.
        await Task.Delay(2500);

        return _agent.GenerateAnswer(pInfo.Query, data);
    }
}
