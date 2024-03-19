using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ShellCopilot.Azure;

public class MetricHelper
{
    public static readonly bool TelemetryOptOut = GetEnvironmentVariableAsBool("COPILOT_TELEMETRY_OPTOUT", false);

    private string _endpoint;
    private TelemetryClient _telemetryClient;

    public MetricHelper(string endpoint)
    {
        _endpoint = endpoint;
        InitializeTelemetryClient();
    }

    private void InitializeTelemetryClient()
    {
        // Create the DI container.
        IServiceCollection services = new ServiceCollection();

        // Add custom TelemetryInitializer
        services.AddSingleton(typeof(ITelemetryInitializer), new MyCustomTelemetryInitializer(_endpoint));

        // Configure TelemetryConfiguration
        services.Configure<TelemetryConfiguration>(config =>
        {
            // Optionally configure AAD
            // var credential = new DefaultAzureCredential();
            // config.SetAzureTokenCredential(credential);
        });

        // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
        // Hence connection string must be specified here.
        services.AddApplicationInsightsTelemetryWorkerService((ApplicationInsightsServiceOptions options) => options.ConnectionString = "InstrumentationKey=c7d054ff-9f40-43e8-bf8e-7d76c58cc1af");

        // Add custom TelemetryProcessor
        services.AddApplicationInsightsTelemetryProcessor<MyCustomTelemetryProcessor>();

        // Build ServiceProvider.
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
        _telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();
    }

    public void LogTelemetry(AzTrace trace)
    {
        Dictionary<string, string> eventProperties = new()
        {
            { "CorrelationID", trace.CorrelationID },
            { "InstallationID", trace.InstallationID },
            { "Handler", trace.Handler },
            { "EventType", trace.EventType },
            { "Duration", trace.Duration?.ToString() },
            { "Command", trace.Command },
            { "DetailedMessage", trace.DetailedMessage },
            { "HistoryMessage", JsonSerializer.Serialize(trace.HistoryMessage) },
            { "StartTime", trace.StartTime?.ToString() },
            { "EndTime", trace.EndTime?.ToString() },
        };

        _telemetryClient.TrackTrace("shellCopilot", eventProperties);

        // Explicitly call Flush() followed by sleep is required in Console Apps.
        // This is to ensure that even if application terminates, telemetry is sent to the back-end.
        _telemetryClient.Flush();
        // Task.Delay(500000).Wait();
    }

    private static bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
    {
        var str = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(str))
        {
            return defaultValue;
        }

        if (bool.TryParse(str, out bool result))
        {
            return result;
        }

        if (string.Equals(str, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(str, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return defaultValue;
    }
}

internal class MyCustomTelemetryInitializer : ITelemetryInitializer
{
    private string _endpoint;
    public void Initialize(ITelemetry telemetry)
    {
        // Replace with actual properties.
        (telemetry as ISupportProperties).Properties["Endpoint"] = _endpoint;
        telemetry.Context.Cloud.RoleInstance = "Not Available";
    }

    public MyCustomTelemetryInitializer(string endpoint) 
    {
        _endpoint = endpoint;
    }
}

internal class MyCustomTelemetryProcessor : ITelemetryProcessor
{
    ITelemetryProcessor next;

    public MyCustomTelemetryProcessor(ITelemetryProcessor next)
    {
        this.next = next;

    }
    public void Process(ITelemetry item)
    {
        // Example processor - not filtering out anything.
        // This should be replaced with actual logic.
        this.next.Process(item);
    }
}
