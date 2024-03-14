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
    private string _endpoint;
    private TelemetryClient _telemetryClient;
    public bool _disableAzureDataCollection;

    public MetricHelper(string endpoint) 
    {
        _endpoint = endpoint;
        _disableAzureDataCollection = GetEnvironmentVariableAsBool("COPILOT_TELEMETRY_OPTOUT", true);
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
        if (string.IsNullOrEmpty(str))
        {
            return defaultValue;
        }

        var boolStr = str.AsSpan();

        if (boolStr.Length == 1)
        {
            if (boolStr[0] == '1')
            {
                return true;
            }

            if (boolStr[0] == '0')
            {
                return false;
            }
        }

        if (boolStr.Length == 3 &&
            (boolStr[0] == 'y' || boolStr[0] == 'Y') &&
            (boolStr[1] == 'e' || boolStr[1] == 'E') &&
            (boolStr[2] == 's' || boolStr[2] == 'S'))
        {
            return true;
        }

        if (boolStr.Length == 2 &&
            (boolStr[0] == 'n' || boolStr[0] == 'N') &&
            (boolStr[1] == 'o' || boolStr[1] == 'O'))
        {
            return false;
        }

        if (boolStr.Length == 4 &&
            (boolStr[0] == 't' || boolStr[0] == 'T') &&
            (boolStr[1] == 'r' || boolStr[1] == 'R') &&
            (boolStr[2] == 'u' || boolStr[2] == 'U') &&
            (boolStr[3] == 'e' || boolStr[3] == 'E'))
        {
            return true;
        }

        if (boolStr.Length == 5 &&
            (boolStr[0] == 'f' || boolStr[0] == 'F') &&
            (boolStr[1] == 'a' || boolStr[1] == 'A') &&
            (boolStr[2] == 'l' || boolStr[2] == 'L') &&
            (boolStr[3] == 's' || boolStr[3] == 'S') &&
            (boolStr[4] == 'e' || boolStr[4] == 'E'))
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
