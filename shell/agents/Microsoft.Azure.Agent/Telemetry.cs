using System.Text.Json;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Agent;

public class AzTrace
{
    private static readonly string s_installationId;
    static AzTrace()
    {
        string azureConfigDir = Environment.GetEnvironmentVariable("AZURE_CONFIG_DIR");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userProfilePath = Path.Combine(string.IsNullOrEmpty(azureConfigDir) ? userProfile : azureConfigDir, "azureProfile.json");

        JsonElement array;
        s_installationId = null;

        if (File.Exists(userProfilePath))
        {
            using var jsonStream = new FileStream(userProfilePath, FileMode.Open, FileAccess.Read);
            array = JsonSerializer.Deserialize<JsonElement>(jsonStream);
            s_installationId = array.GetProperty("installationId").GetString();
        }
        else
        {
            try
            {
                Path.Combine(string.IsNullOrEmpty(azureConfigDir) ? userProfile : azureConfigDir, "AzureRmContextSettings.json");
                using var jsonStream = new FileStream(userProfilePath, FileMode.Open, FileAccess.Read);
                array = JsonSerializer.Deserialize<JsonElement>(jsonStream);
                s_installationId = array.GetProperty("Settings").GetProperty("InstallationId").GetString();
            }
            catch
            {
                // If finally no installation id found, just return null.
                s_installationId = null;
            }
        }
    }

    internal AzTrace()
    {
        InstallationId = s_installationId;
    }

    /// <summary>
    /// Installation id from the Azure CLI installation.
    /// </summary>
    internal string InstallationId { get; }

    /// <summary>
    /// Topic name of the response from Azure Copilot.
    /// </summary>
    internal string TopicName { get; set; }

    /// <summary>
    /// Each chat has a unique conversation id. When the customer runs '/refresh',
    /// a new chat will be initiated (i.e. a new conversation id will be created).
    /// </summary>
    internal string ConversationId { get; set; }

    /// <summary>
    /// The activity id of the user's query.
    /// </summary>
    internal string QueryId { get; set; }

    /// <summary>
    /// The event type of this telemetry.
    /// </summary>
    internal string EventType { get; set; }

    /// <summary>
    /// The shell command that triggered this telemetry.
    /// </summary>
    internal string ShellCommand { get; set; }

    /// <summary>
    /// Detailed information.
    /// </summary>
    internal object Details { get; set; }

    internal static AzTrace UserAction(
        string shellCommand,
        CopilotResponse response,
        object details,
        bool isFeedback = false)
    {
        if (Telemetry.Enabled)
        {
            return new()
            {
                QueryId = response.ReplyToId,
                TopicName = response.TopicName,
                ConversationId = response.ConversationId,
                ShellCommand = shellCommand,
                EventType = isFeedback ? "Feedback" : "UserAction",
                Details = details
            };
        }

        // Don't create an object when telemetry is disabled.
        return null;
    }

    internal static AzTrace Chat(CopilotResponse response)
    {
        if (Telemetry.Enabled)
        {
            return new()
            {
                EventType = "Chat",
                QueryId = response.ReplyToId,
                TopicName = response.TopicName,
                ConversationId = response.ConversationId
            };
        }

        // Don't create an object when telemetry is disabled.
        return null;
    }

    internal static AzTrace Exception(CopilotResponse response, object details)
    {
        if (Telemetry.Enabled)
        {
            return new()
            {
                EventType = "Exception",
                QueryId = response?.ReplyToId,
                TopicName = response?.TopicName,
                ConversationId = response?.ConversationId,
                Details = details
            };
        }

        // Don't create an object when telemetry is disabled.
        return null;
    }
}

internal class Telemetry
{
    private static Telemetry s_singleton;
    private readonly TelemetryClient _telemetryClient;

    private Telemetry()
    {
        // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
        // Hence connection string must be specified here.
        IServiceCollection services = new ServiceCollection()
            .AddApplicationInsightsTelemetryWorkerService((ApplicationInsightsServiceOptions options) =>
                {
                    // Application insights in the temp environment.
                    options.ConnectionString = "InstrumentationKey=eea660a1-d969-44f8-abe4-96666e7fb159";
                    options.EnableHeartbeat = false;
                    options.EnableDiagnosticsTelemetryModule = false;
                });

        // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
        _telemetryClient = services
            .BuildServiceProvider()
            .GetRequiredService<TelemetryClient>();

        // Suppress the PII recorded by default to reduce risk.
        _telemetryClient.Context.Cloud.RoleInstance = "Not Available";
    }

    private void LogTelemetry(AzTrace trace)
    {
        Dictionary<string, string> telemetryEvent = new()
        {
            ["QueryId"] = trace.QueryId,
            ["ConversationId"] = trace.ConversationId,
            ["InstallationId"] = trace.InstallationId,
            ["TopicName"] = trace.TopicName,
            ["EventType"] = trace.EventType,
            ["ShellCommand"] = trace.ShellCommand,
            ["Details"] = GetDetailedMessage(trace.Details),
        };

        _telemetryClient.TrackTrace("AIShell-Test1022", telemetryEvent);
        _telemetryClient.Flush();
    }

    private static string GetDetailedMessage(object details)
    {
        if (details is null)
        {
            return null;
        }

        if (details is string str)
        {
            return str;
        }

        return JsonSerializer.Serialize(details, Utils.RelaxedJsonEscapingOptions);
    }

    /// <summary>
    /// Gets whether or not telemetry is enabled.
    /// </summary>
    internal static bool Enabled => s_singleton is not null;

    /// <summary>
    /// Initialize telemetry client.
    /// </summary>
    internal static void Initialize() => s_singleton ??= new Telemetry();

    /// <summary>
    /// Trace a telemetry metric.
    /// The method does nothing when it's disabled.
    /// </summary>
    internal static void Trace(AzTrace trace) => s_singleton?.LogTelemetry(trace);
}
