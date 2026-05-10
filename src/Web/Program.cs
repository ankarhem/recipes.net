using App.Notifications;
using Infrastructure.Notifications;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;
using Web.Serialization;
using Web.Settings;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Iso8601DurationConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableIso8601DurationConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<INotificationClient, NotificationClient>();
builder.Services.AddProblemDetails();

var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource
            .AddService(
                serviceName: appSettings.ServiceName,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
            )
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                }
            )
    )
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TracingInterceptor.ClientSource.Name)
            .AddSource(TracingInterceptor.WorkflowsSource.Name)
            .AddSource(TracingInterceptor.ActivitiesSource.Name)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/healthcheck");
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            });

        if (appSettings.Otel.ConsoleExporterEnabled)
        {
            tracing.AddConsoleExporter();
        }

        if (appSettings.Otel.OtlpEndpoint is not null)
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(appSettings.Otel.OtlpEndpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (appSettings.Otel.ConsoleExporterEnabled)
        {
            metrics.AddConsoleExporter(
                (_, readerOptions) =>
                {
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                        10_000;
                }
            );
        }

        if (appSettings.Otel.OtlpEndpoint is not null)
        {
            metrics.AddOtlpExporter(
                (exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(appSettings.Otel.OtlpEndpoint);
                    readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                }
            );
        }
    });

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = appSettings.Temporal.Target;
    options.Namespace = appSettings.Temporal.Namespace;
    options.Interceptors = new[] { new TracingInterceptor() };
});

builder.Services.AddSingleton<INotificationService>(sp => new NotificationService(
    sp.GetRequiredService<ILogger<NotificationService>>(),
    sp.GetRequiredService<ITemporalClient>(),
    appSettings.Temporal.TaskQueue
));

builder
    .Services.AddHostedTemporalWorker(appSettings.Temporal.TaskQueue)
    .AddTransientActivities<NotificationActivities>()
    .AddWorkflow<NotificationWorkflow>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}/openapi.{yaml|json}");
    app.MapScalarApiReference(
        "/docs/v1",
        options => options.WithOpenApiRoutePattern("/openapi/{documentName}/openapi.yaml")
    );
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();

app.Run();

public partial class Program;
