using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Agent;

public sealed class MetricHelper
{
    public static readonly bool TelemetryOptOut = GetEnvironmentVariableAsBool("COPILOT_TELEMETRY_OPTOUT", false);
    private const int CustomDomainMaximumSize = 8192;
    private TelemetryClient _telemetryClient;

    private static readonly Lazy<MetricHelper> lazy =
        new Lazy<MetricHelper>(() => new MetricHelper());

    public static MetricHelper metricHelper { get { return lazy.Value; } }

    private MetricHelper()
    {
        InitializeTelemetryClient();
    }

    private void InitializeTelemetryClient()
    {
        // Create the DI container.
        IServiceCollection services = new ServiceCollection();

        // Add custom TelemetryInitializer
        services.AddSingleton(typeof(ITelemetryInitializer), new MyCustomTelemetryInitializer());

        // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
        // Hence connection string must be specified here.
        services.AddApplicationInsightsTelemetryWorkerService((ApplicationInsightsServiceOptions options) =>
            {
                // Application insights in the temp environment.
                options.ConnectionString = "InstrumentationKey=eea660a1-d969-44f8-abe4-96666e7fb159";
                options.EnableHeartbeat = false;
                options.EnableDiagnosticsTelemetryModule = false;
            }
        );

        // Build ServiceProvider.
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
        _telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();

        // Suppress the PII recorded by default to reduce risk.
        _telemetryClient.Context.Cloud.RoleInstance = "Not Available";
    }

    public void LogTelemetry(AzTrace trace)
    {
        Dictionary<string, string> eventProperties = new()
        {
            { "ActivityId", trace.ActivityId},
            { "CoversationId", trace.ConversationId },
            { "InstallationId", trace.InstallationId },
            { "Handler", trace.TopicName },
            { "EventType", trace.EventType },
            { "Command", trace.Command },
            { "DetailedMessage", trace.DetailedMessage }
        };

        _telemetryClient.TrackTrace("AIShell-Test1022", eventProperties);
        _telemetryClient.Flush();
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
    public void Initialize(ITelemetry telemetry){}

    public MyCustomTelemetryInitializer() {}
}
