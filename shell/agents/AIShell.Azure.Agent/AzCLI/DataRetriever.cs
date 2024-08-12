using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIShell.Abstraction;

namespace AIShell.Azure.CLI;

internal class DataRetriever : IDisposable
{
    private static readonly Dictionary<string, NamingRule> s_azNamingRules;
    private static readonly ConcurrentDictionary<string, Command> s_azStaticDataCache;

    private readonly string _staticDataRoot;
    private readonly Task _rootTask;
    private readonly SemaphoreSlim _semaphore;
    private readonly List<ArgumentPair> _placeholders;
    private readonly Dictionary<string, ArgumentPair> _placeholderMap;

    private bool _stop;

    static DataRetriever()
    {
        List<NamingRule> rules = [
            new("API Management Service",
                "apim",
                "The name only allows alphanumeric characters and hyphens, and the first character must be a letter. Length: 1 to 50 chars.",
                "az apim create --name",
                "New-AzApiManagement -Name"),

            new("Function App",
                "func",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 2 to 60 chars.",
                "az functionapp create --name",
                "New-AzFunctionApp -Name"),

            new("App Service Plan",
                "asp",
                "The name only allows alphanumeric characters and hyphens. Length: 1 to 60 chars.",
                "az appservice plan create --name",
                "New-AzAppServicePlan -Name"),

            new("Web App",
                "web",
                "The name only allows alphanumeric characters and hyphens. Length: 2 to 43 chars.",
                "az webapp create --name",
                "New-AzWebApp -Name"),

            new("Application Gateway",
                "agw",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 - 80 chars.",
                "az network application-gateway create --name",
                "New-AzApplicationGateway -Name"),

            new("Application Insights",
                "ai",
                "The name only allows alphanumeric characters, underscores, periods, hyphens and parenthesis, and cannot end in a period. Length: 1 to 255 chars.",
                "az monitor app-insights component create --app",
                "New-AzApplicationInsights -Name"),

            new("Application Security Group",
                "asg",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network asg create --name",
                "New-AzApplicationSecurityGroup -Name"),

            new("Automation Account",
                "aa",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 6 to 50 chars.",
                "az automation account create --name",
                "New-AzAutomationAccount -Name"),

            new("Availability Set",
                "as",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az vm availability-set create --name",
                "New-AzAvailabilitySet -Name"),

            new("Redis Cache",
                "redis",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Consecutive hyphens are not allowed. Length: 1 to 63 chars.",
                "az redis create --name",
                "New-AzRedisCache -Name"),

            new("Cognitive Service",
                "cogs",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 2 to 64 chars.",
                "az cognitiveservices account create --name",
                "New-AzCognitiveServicesAccount -Name"),

            new("Cosmos DB",
                "cosmos",
                "The name only allows lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen. Length: 3 to 44 chars.",
                "az cosmosdb create --name",
                "New-AzCosmosDBAccount -Name"),

            new("Event Hubs Namespace",
                "eh",
                "The name only allows alphanumeric characters and hyphens. It must start with a letter and end with a letter or number. Length: 6 to 50 chars.",
                "az eventhubs namespace create --name",
                "New-AzEventHubNamespace -Name"),

            new("Event Hubs",
                abbreviation: null,
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start and end with a letter or number. Length: 1 to 256 chars.",
                "az eventhubs eventhub create --name",
                "New-AzEventHub -Name"),

            new("Key Vault",
                "kv",
                "The name only allows alphanumeric characters and hyphens. It must start with a letter and end with a letter or number. Consecutive hyphens are not allowed. Length: 3 to 24 chars.",
                "az keyvault create --name",
                "New-AzKeyVault -Name"),

            new("Load Balancer",
                "lb",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network lb create --name",
                "New-AzLoadBalancer -Name"),

            new("Log Analytics workspace",
                "la",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 4 to 63 chars.",
                "az monitor log-analytics workspace create --name",
                "New-AzOperationalInsightsWorkspace -Name"),

            new("Logic App",
                "lapp",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 2 to 64 chars.",
                "az logic workflow create --name",
                "New-AzLogicApp -Name"),

            new("Machine Learning workspace",
                "mlw",
                "The name only allows alphanumeric characters, underscores, and hyphens. It must start with a letter or number. Length: 3 to 33 chars.",
                "az ml workspace create --name",
                "New-AzMLWorkspace -Name"),

            new("Network Interface",
                "nic",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 2 to 64 chars.",
                "az network nic create --name",
                "New-AzNetworkInterface -Name"),

            new("Network Security Group",
                "nsg",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 2 to 64 chars.",
                "az network nsg create --name",
                "New-AzNetworkSecurityGroup -Name"),

            new("Notification Hub Namespace",
                "nh",
                "The name only allows alphanumeric characters and hyphens. It must start with a letter and end with a letter or number. Length: 6 to 50 chars.",
                "az notification-hub namespace create --name",
                "New-AzNotificationHubsNamespace -Namespace"),

            new("Notification Hub",
                abbreviation: null,
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start and end with a letter or number. Length: 1 to 260 chars.",
                "az notification-hub create --name",
                "New-AzNotificationHub -Name"),

            new("Public IP address",
                "pip",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network public-ip create --name",
                "New-AzPublicIpAddress -Name"),

            new("Resource Group",
                "rg",
                "Resource group names can only include alphanumeric, underscore, parentheses, hyphen, period (except at end), and Unicode characters that match the allowed characters. Length: 1 to 90 chars.",
                "az group create --name",
                "New-AzResourceGroup -Name"),

            new("Route table",
                "rt",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start and end with a letter or number. Length: 1 to 80 chars.",
                "az network route-table create --name",
                "New-AzRouteTable -Name"),

            new("Search Service",
                "srch",
                "Service name must only contain lowercase letters, digits or dashes, cannot use dash as the first two or last one characters, and cannot contain consecutive dashes. Length: 2 to 60 chars.",
                "az search service create --name",
                "New-AzSearchService -Name"),

            new("Service Bus Namespace",
                "sb",
                "The name only allows alphanumeric characters and hyphens. It must start with a letter and end with a letter or number. Length: 6 to 50 chars.",
                "az servicebus namespace create --name",
                "New-AzServiceBusNamespace -Name"),

            new("Service Bus queue",
                abbreviation: null,
                "The name only allows alphanumeric characters and hyphens. It must start with a letter and end with a letter or number. Length: 6 to 50 chars.",
                "az servicebus queue create --name",
                "New-AzServiceBusQueue -Name"),

            new("Azure SQL Managed Instance",
                "sqlmi",
                "The name can only contain lowercase letters, numbers and hyphens. It cannot start or end with a hyphen, nor can it have two consecutive hyphens in the third and fourth places of the name. Length: 1 to 63 chars.",
                "az sql mi create --name",
                "New-AzSqlInstance -Name"),

            new("SQL Server",
                "sqldb",
                "The name can only contain lowercase letters, numbers and hyphens. It cannot start or end with a hyphen, nor can it have two consecutive hyphens in the third and fourth places of the name. Length: 1 to 63 chars.",
                "az sql server create --name",
                "New-AzSqlServer -ServerName"),

            new("Storage Container",
                abbreviation: null,
                "The name can only contain lowercase letters, numbers and hyphens. It must start with a letter or a number, and each hyphen must be preceded and followed by a non-hyphen character. Length: 3 to 63 chars.",
                "az storage container create --name",
                "New-AzStorageContainer -Name"),

            new("Storage Queue",
                abbreviation: null,
                "The name can only contain lowercase letters, numbers and hyphens. It must start with a letter or a number, and each hyphen must be preceded and followed by a non-hyphen character. Length: 3 to 63 chars.",
                "az storage queue create --name",
                "New-AzStorageQueue -Name"),

            new("Storage Table",
                abbreviation: null,
                "The name can only contain letters and numbers, and must start with a letter. Length: 3 to 63 chars.",
                "az storage table create --name",
                "New-AzStorageTable -Name"),

            new("Storage File Share",
                abbreviation: null,
                "The name can only contain lowercase letters, numbers and hyphens. It must start and end with a letter or number, and cannot contain two consecutive hyphens. Length: 3 to 63 chars.",
                "az storage share create --name",
                "New-AzStorageShare -Name"),

            new("Container Registry",
                "cr",
                "The name only allows alphanumeric characters. Length: 5 to 50 chars.",
                "cr<product>[<environment>][<identifier>]",
                ["crnavigatorprod001", "crhadoopdev001"],
                "az acr create --name",
                "New-AzContainerRegistry -Name"),

            new("Storage Account",
                "st",
                "The name can only contain lowercase letters and numbers. Length: 3 to 24 chars.",
                "st<product>[<environment>][<identifier>]",
                ["stsalesappdataqa", "sthadoopoutputtest"],
                "az storage account create --name",
                "New-AzStorageAccount -Name"),

            new("Traffic Manager profile",
                "tm",
                "The name only allows alphanumeric characters and hyphens, and cannot start or end with a hyphen. Length: 1 to 63 chars.",
                "az network traffic-manager profile create --name",
                "New-AzTrafficManagerProfile -Name"),

            new("Virtual Machine",
                "vm",
                @"The name cannot contain special characters \/""[]:|<>+=;,?*@&#%, whitespace, or begin with '_' or end with '.' or '-'. Length: 1 to 15 chars for Windows; 1 to 64 chars for Linux.",
                "az vm create --name",
                "New-AzVM -Name"),

            new("Virtual Network Gateway",
                "vgw",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network vnet-gateway create --name",
                "New-AzVirtualNetworkGateway -Name"),

            new("Local Network Gateway",
                "lgw",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network local-gateway create --name",
                "New-AzLocalNetworkGateway -Name"),

            new("Virtual Network",
                "vnet",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network vnet create --name",
                "New-AzVirtualNetwork -Name"),

            new("Subnet",
                "snet",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network vnet subnet create --name",
                "Add-AzVirtualNetworkSubnetConfig -Name"),

            new("VPN Connection",
                "vcn",
                "The name only allows alphanumeric characters, underscores, periods, and hyphens. It must start with a letter or number, and end with a letter, number or underscore. Length: 1 to 80 chars.",
                "az network vpn-connection create --name",
                "New-AzVpnConnection -Name"),
        ];

        s_azNamingRules = new(capacity: rules.Count * 2, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            s_azNamingRules.Add(rule.AzCLICommand, rule);
            s_azNamingRules.Add(rule.AzPSCommand, rule);
        }

        s_azStaticDataCache = new(StringComparer.OrdinalIgnoreCase);
    }

    internal DataRetriever(ResponseData data)
    {
        _stop = false;
        _semaphore = new SemaphoreSlim(3, 3);
        _staticDataRoot = @"E:\yard\tmp\az-cli-out\az";
        _placeholders = new(capacity: data.PlaceholderSet.Count);
        _placeholderMap = new(capacity: data.PlaceholderSet.Count);

        PairPlaceholders(data);
        _rootTask = Task.Run(StartProcessing);
    }

    private void PairPlaceholders(ResponseData data)
    {
        var cmds = new Dictionary<string, string>(data.CommandSet.Count);

        foreach (var item in data.PlaceholderSet)
        {
            string command = null, parameter = null;

            foreach (var cmd in data.CommandSet)
            {
                string script = cmd.Script;

                // Handle AzCLI commands.
                if (script.StartsWith("az ", StringComparison.OrdinalIgnoreCase))
                {
                    if (!cmds.TryGetValue(script, out command))
                    {
                        int firstParamIndex = script.IndexOf("--");
                        command = script.AsSpan(0, firstParamIndex).Trim().ToString();
                        cmds.Add(script, command);
                    }

                    int argIndex = script.IndexOf(item.Name, StringComparison.OrdinalIgnoreCase);
                    if (argIndex is -1)
                    {
                        continue;
                    }

                    int paramIndex = script.LastIndexOf("--", argIndex);
                    parameter = script.AsSpan(paramIndex, argIndex - paramIndex).Trim().ToString();

                    break;
                }

                // It's a non-AzCLI command, such as "ssh".
                if (script.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Leave the parameter to be null for non-AzCLI commands, as there is
                    // no reliable way to parse an arbitrary command
                    command = script;
                    parameter = null;

                    break;
                }
            }

            ArgumentPair pair = new(item, command, parameter);
            _placeholders.Add(pair);
            _placeholderMap.Add(item.Name, pair);
        }
    }

    private void StartProcessing()
    {
        foreach (var pair in _placeholders)
        {
            if (_stop) { break; }

            _semaphore.Wait();

            if (pair.ArgumentInfo is null)
            {
                lock (pair)
                {
                    if (pair.ArgumentInfo is null)
                    {
                        pair.ArgumentInfo = Task.Factory.StartNew(ProcessOne, pair);
                        continue;
                    }
                }
            }

            _semaphore.Release();
        }

        ArgumentInfo ProcessOne(object pair)
        {
            try
            {
                return CreateArgInfo((ArgumentPair)pair);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    private ArgumentInfo CreateArgInfo(ArgumentPair pair)
    {
        var item = pair.Placeholder;
        var dataType = Enum.Parse<ArgumentInfo.DataType>(item.Type, ignoreCase: true);

        if (item.ValidValues?.Count > 0)
        {
            return new ArgumentInfo(item.Name, item.Desc, restriction: null, dataType, item.ValidValues);
        }

        // Handle non-AzCLI command.
        if (pair.Parameter is null)
        {
            return new ArgumentInfo(item.Name, item.Desc, dataType);
        }

        string cmdAndParam = $"{pair.Command} {pair.Parameter}";
        if (s_azNamingRules.TryGetValue(cmdAndParam, out NamingRule rule))
        {
            string restriction = rule.PatternText is null
                ? rule.GeneralRule
                : $"""
                     - {rule.GeneralRule}
                     - Recommended pattern: {rule.PatternText}, e.g. {string.Join(", ", rule.Example)}.
                    """;
            return new ArgumentInfoWithNamingRule(item.Name, item.Desc, restriction, rule);
        }

        if (string.Equals(pair.Parameter, "--name", StringComparison.OrdinalIgnoreCase)
            && pair.Command.EndsWith(" create", StringComparison.OrdinalIgnoreCase))
        {
            // Placeholder is for the name of a new resource to be created, but not in our cache.
            return new ArgumentInfo(item.Name, item.Desc, dataType);
        }

        if (_stop) { return null; }

        List<string> suggestions = GetArgValues(pair, out Option option);
        // If the option's description is less than the placeholder's description in length, then it's
        // unlikely to provide more information than the latter. In that case, we don't use it.
        string optionDesc = option?.Description?.Length > item.Desc.Length ? option.Description : null;
        return new ArgumentInfo(item.Name, item.Desc, optionDesc, dataType, suggestions);
    }

    private List<string> GetArgValues(ArgumentPair pair, out Option option)
    {
        // First, try to get static argument values if they exist.
        string command = pair.Command;
        if (!s_azStaticDataCache.TryGetValue(command, out Command commandData))
        {
            string[] cmdElements = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string dirPath = _staticDataRoot;
            for (int i = 1; i < cmdElements.Length - 1; i++)
            {
                dirPath = Path.Combine(dirPath, cmdElements[i]);
            }

            string filePath = Path.Combine(dirPath, cmdElements[^1] + ".json");
            commandData = File.Exists(filePath)
                ? JsonSerializer.Deserialize<Command>(File.OpenRead(filePath))
                : null;
            s_azStaticDataCache.TryAdd(command, commandData);
        }

        option = commandData?.FindOption(pair.Parameter);
        List<string> staticValues = option?.Arguments;
        if (staticValues?.Count > 0)
        {
            return staticValues;
        }

        if (_stop) { return null; }

        // Then, try to get dynamic argument values using AzCLI tab completion.
        string commandLine = $"{pair.Command} {pair.Parameter} ";
        string tempFile = Path.GetTempFileName();

        try
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = @"C:\Program Files\Microsoft SDKs\Azure\CLI2\python.exe",
                    Arguments = "-Im azure.cli",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            var env = process.StartInfo.Environment;
            env.Add("ARGCOMPLETE_USE_TEMPFILES", "1");
            env.Add("_ARGCOMPLETE_STDOUT_FILENAME", tempFile);
            env.Add("COMP_LINE", commandLine);
            env.Add("COMP_POINT", (commandLine.Length + 1).ToString());
            env.Add("_ARGCOMPLETE", "1");
            env.Add("_ARGCOMPLETE_SUPPRESS_SPACE", "0");
            env.Add("_ARGCOMPLETE_IFS", "\n");
            env.Add("_ARGCOMPLETE_SHELL", "powershell");

            process.Start();
            process.WaitForExit();

            string line;
            using FileStream stream = File.OpenRead(tempFile);
            if (stream.Length is 0)
            {
                // No allowed values for the option.
                return null;
            }

            using StreamReader reader = new(stream);
            List<string> output = [];

            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith('-'))
                {
                    // Argument completion generates incorrect results -- options are written into the file instead of argument allowed values.
                    return null;
                }

                string value = line.Trim();
                if (value != string.Empty)
                {
                    output.Add(value);
                }
            }

            return output.Count > 0 ? output : null;
        }
        catch (Win32Exception e)
        {
            throw new ApplicationException($"Failed to get allowed values for 'az {commandLine}': {e.Message}", e);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal (string command, string parameter) GetMappedCommand(string placeholderName)
    {
        if (_placeholderMap.TryGetValue(placeholderName, out ArgumentPair pair))
        {
            return (pair.Command, pair.Parameter);
        }

        throw new ArgumentException($"Unknown placeholder name: '{placeholderName}'", nameof(placeholderName));
    }

    internal Task<ArgumentInfo> GetArgInfo(string placeholderName)
    {
        if (_placeholderMap.TryGetValue(placeholderName, out ArgumentPair pair))
        {
            if (pair.ArgumentInfo is null)
            {
                lock (pair)
                {
                    pair.ArgumentInfo ??= Task.Run(() => CreateArgInfo(pair));
                }
            }

            return pair.ArgumentInfo;
        }

        throw new ArgumentException($"Unknown placeholder name: '{placeholderName}'", nameof(placeholderName));
    }

    public void Dispose()
    {
        _stop = true;
        _rootTask.Wait();
        _semaphore.Dispose();
    }
}

internal class ArgumentPair
{
    internal PlaceholderItem Placeholder { get; }
    internal string Command { get; }
    internal string Parameter { get; }
    internal Task<ArgumentInfo> ArgumentInfo { set; get; }

    internal ArgumentPair(PlaceholderItem placeholder, string command, string parameter)
    {
        Placeholder = placeholder;
        Command = command;
        Parameter = parameter;
        ArgumentInfo = null;
    }
}

internal class ArgumentInfoWithNamingRule : ArgumentInfo
{
    internal ArgumentInfoWithNamingRule(string name, string description, string restriction, NamingRule rule)
        : base(name, description, restriction, DataType.@string, suggestions: [])
    {
        ArgumentNullException.ThrowIfNull(rule);
        NamingRule = rule;
    }

    internal NamingRule NamingRule { get; }
}

internal class NamingRule
{
    private static readonly string[] s_products = ["salesapp", "bookingweb", "navigator", "hadoop", "sharepoint"];
    private static readonly string[] s_envs = ["prod", "dev", "qa", "stage", "test"];

    internal string ResourceName { get; }
    internal string Abbreviation { get; }
    internal string GeneralRule { get; }
    internal string PatternText { get; }
    internal Regex PatternRegex { get; }
    internal string[] Example { get; }

    internal string AzCLICommand { get; }
    internal string AzPSCommand { get; }

    internal NamingRule(
        string resourceName,
        string abbreviation,
        string generalRule,
        string azCLICommand,
        string azPSCommand)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentException.ThrowIfNullOrEmpty(generalRule);
        ArgumentException.ThrowIfNullOrEmpty(azCLICommand);
        ArgumentException.ThrowIfNullOrEmpty(azPSCommand);

        ResourceName = resourceName;
        Abbreviation = abbreviation;
        GeneralRule = generalRule;
        AzCLICommand = azCLICommand;
        AzPSCommand = azPSCommand;

        if (abbreviation is not null)
        {
            PatternText = $"<product>-{abbreviation}[-<environment>][-<identifier>]";
            PatternRegex = new Regex($"^(?<prod>[a-zA-Z0-9]+)-{abbreviation}(?:-(?<env>[a-zA-Z0-9]+))?(?:-[a-zA-Z0-9]+)?$", RegexOptions.Compiled);

            string product = s_products[Random.Shared.Next(0, s_products.Length)];
            int envIndex = Random.Shared.Next(0, s_envs.Length);
            Example = [$"{product}-{abbreviation}-{s_envs[envIndex]}", $"{product}-{abbreviation}-{s_envs[(envIndex + 1) % s_envs.Length]}"];
        }
    }

    internal NamingRule(
        string resourceName,
        string abbreviation,
        string generalRule,
        string patternText,
        string[] example,
        string azCLICommand,
        string azPSCommand)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentException.ThrowIfNullOrEmpty(generalRule);
        ArgumentException.ThrowIfNullOrEmpty(azCLICommand);
        ArgumentException.ThrowIfNullOrEmpty(azPSCommand);

        ResourceName = resourceName;
        Abbreviation = abbreviation;
        GeneralRule = generalRule;
        PatternText = patternText;
        PatternRegex = null;
        Example = example;

        AzCLICommand = azCLICommand;
        AzPSCommand = azPSCommand;
    }

    internal bool TryMatchName(string name, out string prodName, out string envName)
    {
        prodName = envName = null;
        if (PatternRegex is null)
        {
            return false;
        }

        Match match = PatternRegex.Match(name);
        if (match.Success)
        {
            prodName = match.Groups["prod"].Value;
            envName = match.Groups["env"].Value;
            return true;
        }

        return false;
    }
}

public class Option
{
    public string Name { get; }
    public string[] Alias { get; }
    public string[] Short { get; }
    public string Attribute { get; }
    public string Description { get; set; }
    public List<string> Arguments { get; set; }

    public Option(string name, string description, string[] alias, string[] @short, string attribute, List<string> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(description);

        Name = name;
        Alias = alias;
        Short = @short;
        Attribute = attribute;
        Description = description;
        Arguments = arguments;
    }
}

public sealed class Command
{
    public List<Option> Options { get; }
    public string Examples { get; }
    public string Name { get; }
    public string Description { get; }

    public Command(string name, string description, List<Option> options, string examples)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(description);
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        Examples = examples;
        Name = name;
        Description = description;
    }

    public Option FindOption(string name)
    {
        foreach (Option option in Options)
        {
            if (name.StartsWith("--"))
            {
                if (string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }

                if (option.Alias is not null)
                {
                    foreach (string alias in option.Alias)
                    {
                        if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return option;
                        }
                    }
                }
            }
            else if (option.Short is not null)
            {
                foreach (string s in option.Short)
                {
                    if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return option;
                    }
                }
            }
        }

        return null;
    }
}
