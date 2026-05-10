using App.Notifications;
using Infrastructure.Notifications;
using Scalar.AspNetCore;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
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

var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

builder.Services.AddTemporalClient(appSettings.Temporal.Target, appSettings.Temporal.Namespace);

builder.Services.AddSingleton<INotificationService>(sp => new NotificationService(
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
app.MapControllers();

app.Run();

public partial class Program;
