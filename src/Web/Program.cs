using App.Notifications;
using Infrastructure.Notifications;
using Scalar.AspNetCore;
using Web.Serialization;

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
builder.Services.AddSingleton<INotificationService, NotificationService>();

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
