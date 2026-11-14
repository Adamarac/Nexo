using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexo.Application.Observability;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace Nexo.Infrastructure.Observability;

public static class ObservabilitySetup
{
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Logs em JSON no console e traces/métricas via OpenTelemetry.
    // Se OTEL_EXPORTER_OTLP_ENDPOINT estiver definido, tudo também vai para o Grafana.
    public static IHostApplicationBuilder AddNexoObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var otlpEndpoint = builder.Configuration[OtlpEndpointVariable];

        builder.Services.AddSerilog((services, logger) =>
        {
            logger.ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(new RenderedCompactJsonFormatter());

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                logger.WriteTo.OpenTelemetry(sink =>
                {
                    sink.Endpoint = otlpEndpoint;
                    sink.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName,
                        ["service.instance.id"] = Environment.MachineName,
                    };
                });
            }
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            // A instância leva o nome do container, o mesmo que aparece na conexão com o RabbitMQ.
            .ConfigureResource(resource => resource.AddService(serviceName, serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddSource(NexoTelemetry.Name)
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(NexoTelemetry.Name)
                .AddMeter("System.Runtime")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            telemetry.UseOtlpExporter();

        return builder;
    }
}
