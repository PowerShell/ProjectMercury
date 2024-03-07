﻿using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace ShellCopilot.Azure
{
    public class MetricHelper
    {
        public static string Endpoint;
        public void LogTelemetry(string url, AzTrace trace = null)
        {
            // Identify the Endpoint
            Endpoint = url;
            
            // Create the DI container.
            IServiceCollection services = new ServiceCollection();
            
            // Add custom TelemetryInitializer
            services.AddSingleton<ITelemetryInitializer, MyCustomTelemetryInitializer>();

            // Configure TelemetryConfiguration
            services.Configure<TelemetryConfiguration>(config =>
            {
                // Optionally configure AAD
                //var credential = new DefaultAzureCredential();
                //config.SetAzureTokenCredential(credential);
            });

            // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
            // Hence connection string must be specified here.
            services.AddApplicationInsightsTelemetryWorkerService((ApplicationInsightsServiceOptions options) => options.ConnectionString = "InstrumentationKey=bebe79e3-ad01-4a92-8180-40071f19bd03");

            // Add custom TelemetryProcessor
            services.AddApplicationInsightsTelemetryProcessor<MyCustomTelemetryProcessor>();

            // Example on Configuring TelemetryModules.
            // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Not a real api key, this is example code.")]
            services.ConfigureTelemetryModule<QuickPulseTelemetryModule>((module, opt) => module.AuthenticationApiKey = "4h7fityfa4s8dau3tzxetnvnmtcs176ufv4vd10c");

            // Build ServiceProvider.
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            // Obtain logger instance from DI.
            ILogger<MetricHelper> logger = serviceProvider.GetRequiredService<ILogger<MetricHelper>>();

            // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
            var telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();

            /* Example code for http request - saved in Trace table as well
            var res = new HttpClient().GetAsync(url).Result.StatusCode; // this dependency will be captured by Application Insights.
            logger.LogWarning("Response from bing is:" + res); // this will be captured by Application Insights.
            */

            Dictionary<string, string> eventProperties = new()
            {
                { "CorrelationID", trace?.CorrelationID ?.ToString() },
                { "InstallationID", trace?.InstallationID ?.ToString() },
                { "Handler", trace?.Handler ?? null },
                { "EventType", trace?.EventType ?? null },
                { "Duration", trace?.Duration?.ToString() },
                { "Command", trace?.Command ?? null },
                { "DetailedMessage", trace?.DetailedMessage ?? null },
                { "HistoryMessage", JsonSerializer.Serialize(trace?.HistoryMessage) ?? null },
                { "StartTime", trace?.StartTime?.ToString() },
                { "EndTime", trace?.EndTime?.ToString() },
            };


            telemetryClient.TrackTrace("shellCopilot", eventProperties);

            // Explicitly call Flush() followed by sleep is required in Console Apps.
            // This is to ensure that even if application terminates, telemetry is sent to the back-end.
            telemetryClient.Flush();
            // Task.Delay(500000).Wait();
        }
    }

    internal class MyCustomTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            // Replace with actual properties.
            (telemetry as ISupportProperties).Properties["Endpoint"] = MetricHelper.Endpoint;
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
}