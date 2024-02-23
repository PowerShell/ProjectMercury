using Azure.Identity;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShellCopilot.Azure.Agent.Telemetry
{
    public class MetricHelper
    {
        public void LogTelemetry(string url, AzPSTrace trace = null)
        {
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

            var res = new HttpClient().GetAsync(url).Result.StatusCode; // this dependency will be captured by Application Insights.
            logger.LogWarning("Response from bing is:" + res); // this will be captured by Application Insights.
            Dictionary<string, string> eventProperties = new Dictionary<string, string>();
            //LoadTelemetryClientContext(qos, client.Context);
            //PopulatePropertiesFromQos(qos, eventProperties);
            // qos.Exception contains exception message which may contain Users specific data.
            // We should not collect users specific data.
            if (trace?.CommandType!=null)
            {
                eventProperties.Add("CommandType", trace.CommandType);
            }
            // eventProperties.Add("StackTrace", "");
            // eventProperties.Add("ExceptionType", "");
            logger.LogTrace("logTraceTest", eventProperties);

            telemetryClient.TrackEvent("sampleevent", eventProperties);

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
            (telemetry as ISupportProperties).Properties["MyCustomKey"] = "MyCustomValue";
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