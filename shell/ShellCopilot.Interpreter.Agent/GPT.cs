using System.Diagnostics;
using System.Security;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

internal enum EndpointType
{
    AzureOpenAI,
    OpenAI,
}

public class GPT
{
    internal EndpointType Type { get; }
    internal bool Dirty { set; get; }
    internal ModelInfo ModelInfo { private set; get; }
    public string Endpoint { set; get; }
    public string Deployment { set; get; }
    public string ModelName { set; get; }
    public bool AutoExecution { set; get; }
    public bool DisplayErrors { set; get; }
    public SecureString Key { set; get; }

    public GPT(
        string endpoint,
        string deployment,
        string modelName,
        bool autoExecution,
        bool displayErrors,
        SecureString key)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelName);

        Endpoint = endpoint?.Trim().TrimEnd('/');
        Deployment = deployment;
        ModelName = modelName.ToLowerInvariant();
        AutoExecution = autoExecution;
        DisplayErrors = displayErrors;
        Key = key;

        Dirty = false;
        ModelInfo = ModelInfo.TryResolve(ModelName, out var model) ? model : null;

        bool noEndpoint = string.IsNullOrEmpty(Endpoint);
        bool noDeployment = string.IsNullOrEmpty(Deployment);
        Type = noEndpoint && noDeployment
            ? EndpointType.OpenAI
            : !noEndpoint && !noDeployment
                ? EndpointType.AzureOpenAI
                : throw new InvalidOperationException($"Invalid setting: {(noEndpoint ? "Endpoint" : "Deployment")} key is missing. To use Azure OpenAI service, please specify both the 'Endpoint' and 'Deployment' keys. To use OpenAI service, please ignore both keys.");
    }

    /// <summary>
    /// Self check 
    /// </summary>
    /// <returns></returns>
    internal async Task<bool> SelfCheck(IHost host, CancellationToken token)
    {
        if (Key is not null && ModelInfo is not null)
        {
            return true;
        }

        host.WriteLine()
            .MarkupNoteLine($"Some required information is missing for the GPT:");
        ShowEndpointInfo(host);

        try
        {
            if (ModelInfo is null)
            {
                await AskForModel(host, token);
            }

            if (Key is null)
            {
                await AskForKeyAsync(host, token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            // User cancelled the prompt.
            host.MarkupLine("[red]^C[/]\n");
            return false;
        }
    }

    /// <summary>
    /// Validate the model name and prompt for fixing it if it's invalid.
    /// </summary>
    /// <returns>A boolean value indicates whether the validation and setup was successful.</returns>
    private async Task AskForModel(IHost host, CancellationToken cancellationToken)
    {
        host.MarkupErrorLine($"'{ModelName}' is not a supported OpenAI chat completion model.");
        ModelName = await host.PromptForSelectionAsync(
            title: "Choose from the list of [green]supported OpenAI models[/]:",
            choices: ModelInfo.SupportedModels(),
            cancellationToken: cancellationToken);

        Dirty = true;
        ModelInfo = ModelInfo.GetByName(ModelName);
        host.WriteLine();
    }

    /// <summary>
    /// Prompt for setting up the access key if it doesn't exist.
    /// </summary>
    /// <returns>A boolean value indicates whether the setup was successfully.</returns>
    private async Task AskForKeyAsync(IHost host, CancellationToken cancellationToken)
    {
        host.MarkupNoteLine($"The access key is missing.");
        if (Utils.ShellCopilotEndpoint.Equals(Endpoint, StringComparison.OrdinalIgnoreCase))
        {
            host.MarkupLine(" [grey]> The model uses the default ShellCopilot endpoint.[/]")
                .MarkupLine($" [grey]> You can apply an access key for it by following [green][link={Utils.KeyApplicationHelpLink}]the instructions[/][/] in our doc.[/]\n");
        }

        string secret = await host
            .PromptForSecretAsync("Enter key: ", cancellationToken)
            .ConfigureAwait(false);

        Dirty = true;
        Key = Utils.ConvertToSecureString(secret);
    }

    private void ShowEndpointInfo(IHost host)
    {
        var elements = Type switch
        {
            EndpointType.AzureOpenAI => new CustomElement<GPT>[]
                {
                    new(label: "  Type", m => m.Type.ToString()),
                    new(label: "  Endpoint", m => m.Endpoint),
                    new(label: "  Deployment", m => m.Deployment),
                    new(label: "  Model", m => m.ModelName),
                },

            EndpointType.OpenAI => new CustomElement<GPT>[]
                {
                    new(label: "  Type", m => m.Type.ToString()),
                    new(label: "  Model", m => m.ModelName),
                },

            _ => throw new UnreachableException(),
        };

        host.RenderList(this, elements);
    }
}
