using App.Crawler;
using App.Notifications;
using App.Recipe;
using Infrastructure.Crawler;
using Infrastructure.Notifications;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;
using Web.HealthChecks;
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
builder.Services.AddHealthChecks().AddCheck<TemporalHealthCheck>("temporal", tags: ["ready"]);

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
                    !context.Request.Path.StartsWithSegments("/healthcheck")
                    && !context.Request.Path.StartsWithSegments("/health");
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

builder.Services.AddDbContext<RecipesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Recipes"))
);

builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();

builder.Services.AddHttpClient<ICrawlerClient, CrawlerClient>();
builder.Services.AddSingleton<IScraperService, ScraperService>();
builder.Services.AddSingleton<ICrawlerService>(sp => new CrawlerService(
    sp.GetRequiredService<ILogger<CrawlerService>>(),
    sp.GetRequiredService<ITemporalClient>(),
    appSettings.Temporal.TaskQueue
));

builder
    .Services.AddHostedTemporalWorker(appSettings.Temporal.TaskQueue)
    .AddTransientActivities<NotificationActivities>()
    .AddTransientActivities<CrawlerActivities>()
    .AddWorkflow<NotificationWorkflow>()
    .AddWorkflow<CrawlerWorkflow>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RecipesDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}/openapi.{yaml|json}");
    app.MapScalarApiReference(
        "/docs/v1",
        options => options.WithOpenApiRoutePattern("/openapi/{documentName}/openapi.yaml")
    );
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health/ready");
app.MapControllers();

app.Run();

public partial class Program;
