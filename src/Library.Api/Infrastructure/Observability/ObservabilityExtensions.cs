using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Library.Api.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string HealthPath = "/health";

    /// <summary>
    /// Structured JSON console logs in UTC with scopes, so correlationId and actorId appear on every entry.
    /// </summary>
    public static ILoggingBuilder AddStructuredLogging(this ILoggingBuilder logging)
    {
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        return logging;
    }

    /// <summary>
    /// OpenTelemetry traces, metrics and logs. The OTLP exporter is enabled only when an endpoint is configured,
    /// avoiding export attempts in tests and on machines without a collector.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(Telemetry.ServiceName, serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
                .AddAttributes([new("deployment.environment", environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Telemetry.MeterName))
            .WithLogging(configureBuilder: null, configureOptions: options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
            });

        if (!string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]))
        {
            openTelemetry.UseOtlpExporter();
        }

        return services;
    }
}
