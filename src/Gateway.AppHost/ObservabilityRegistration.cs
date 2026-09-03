using System.Globalization;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace Gateway.AppHost;

/// <summary>
/// Logging and telemetry wiring. A host concern rather than a layer concern, so it lives beside
/// Program.cs rather than in one of the Add* layer registrations.
/// </summary>
internal static class ObservabilityRegistration
{
    private const string ServiceName = "gateway";

    // Matches the bootstrap logger in Program.cs, so startup output does not change shape
    // halfway through boot when the real logger takes over.
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Replaces the default logger with Serilog and, when an OTLP endpoint is configured, exports
    /// traces and metrics.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        AddSerilogLogging(builder);
        AddOpenTelemetry(builder);
    }

    private static void AddSerilogLogging(WebApplicationBuilder builder)
    {
        // Compact JSON everywhere but a developer machine: those logs are read by a collector in
        // compose, and structured output is the whole reason for choosing Serilog over the default
        // logger. On a console someone is actually watching it is unreadable, so Development gets
        // the plain template instead. Serilog:UseJsonConsole overrides the choice either way -
        // set it to true in Development to check what the collector will receive.
        bool useJsonConsole = builder.Configuration.GetValue<bool?>("Serilog:UseJsonConsole")
            ?? !builder.Environment.IsDevelopment();

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName();

            if (useJsonConsole)
            {
                logger.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                // Invariant, not the machine's locale: a log line should read the same on
                // every developer's box.
                logger.WriteTo.Console(
                    outputTemplate: ConsoleTemplate,
                    formatProvider: CultureInfo.InvariantCulture);
            }
        });
    }

    private static void AddOpenTelemetry(WebApplicationBuilder builder)
    {
        string? otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

        // No endpoint, no exporter. Registering it unconditionally would make the service retry a
        // collector that is not there and log an export failure on every interval.
        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return;
        }

        Uri endpoint = new(otlpEndpoint);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()

                // EF Core publishes its own ActivitySource. Subscribing to it directly avoids
                // OpenTelemetry.Instrumentation.EntityFrameworkCore, which is beta-only; the cost
                // is coarser spans without db.statement enrichment.
                .AddSource("Microsoft.EntityFrameworkCore")

                // Emits the graphql.* spans that AddInstrumentation() on the GraphQL builder
                // produces - without that call these would be silent.
                .AddHotChocolateInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));
    }
}
