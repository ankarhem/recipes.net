using App;
using App.Crawler;
using App.Identity;
using App.Recipes;
using Domain.Recipes;
using Infrastructure;
using Infrastructure.Crawler;
using Infrastructure.Identity;
using Infrastructure.Recipes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pgvector.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Extensions.OpenTelemetry;
using Web.ExceptionHandling;
using Web.HealthChecks;
using Web.Serialization;
using Web.Settings;

var builder = WebApplication.CreateBuilder(args);

var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);
builder.Services.AddSingleton(appSettings);

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Iso8601DurationConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableIso8601DurationConverter());
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter<CrawlRunStatus>()
        );
    });
builder.Services.AddOpenApi();
builder.Services.AddHttpLogging();
builder.Services.AddExceptionHandler<LoggingExceptionHandler>();
builder.Services.AddProblemDetails();
builder
    .Services.AddAuthentication(
        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = appSettings.Jwt.Issuer,
            ValidAudience = appSettings.Jwt.Audience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(appSettings.Jwt.SigningKey)
            ),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "auth",
        context =>
        {
            var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }
            );
        }
    );

    options.AddPolicy(
        "crawls",
        context =>
        {
            var partitionKey =
                context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }
            );
        }
    );

    options.AddPolicy(
        "twofa",
        context =>
        {
            var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }
            );
        }
    );
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var knownProxies = builder
        .Configuration.GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>();
    if (knownProxies is not null)
    {
        foreach (var proxyAddress in knownProxies)
        {
            if (System.Net.IPAddress.TryParse(proxyAddress, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
        }
    }
});
builder.Services.AddHealthChecks().AddCheck<TemporalHealthCheck>("temporal", tags: ["ready"]);

if (string.IsNullOrWhiteSpace(appSettings.OpenAi.ApiKey))
{
    throw new InvalidOperationException(
        "OpenAI API key is required. Set the OpenAi:ApiKey configuration value."
    );
}

if (
    string.IsNullOrWhiteSpace(appSettings.Jwt.SigningKey)
    || System.Text.Encoding.UTF8.GetByteCount(appSettings.Jwt.SigningKey) < 32
)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 bytes. Set the Jwt:SigningKey configuration value."
    );
}

if (
    !builder.Environment.IsDevelopment()
    && IsKnownNonProductionJwtSigningKey(appSettings.Jwt.SigningKey)
)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must not use a documented placeholder or development-only value outside Development."
    );
}

if (
    !builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(appSettings.Email.SmtpHost)
)
{
    throw new InvalidOperationException(
        "Email:SmtpHost is required outside Development. Set the Email:SmtpHost configuration value."
    );
}

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

builder.Services.AddDbContext<RecipesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Recipes"), o => o.UseVector())
);

var dataProtectionKeyPath =
    builder.Configuration["DataProtection:KeyPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".dataprotection-keys");

builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("recipes");

builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IRecipeService, App.Recipes.RecipeService>();
builder.Services.AddScoped<IRecipeSearchEmbeddingGenerator, RecipeSearchEmbeddingGenerator>();
builder.Services.AddSingleton<IRecipeEmbeddingTextBuilder, RecipeEmbeddingTextBuilder>();
builder.Services.AddScoped<IRecipeEmbeddingRepository, RecipeEmbeddingRepository>();
builder.Services.AddScoped<IRecipeEmbeddingService, RecipeEmbeddingService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<TotpOptions>(builder.Configuration.GetSection(TotpOptions.SectionName));
builder.Services.AddSingleton<ITotpService, OtpNetTotpService>();
builder.Services.AddSingleton<IQrCodeGenerator, QrCoderGenerator>();
builder.Services.AddSingleton<IRecoveryCodeGenerator, RecoveryCodeGenerator>();
builder.Services.AddScoped<ITotpSecretProtector, DataProtectionTotpSecretProtector>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
builder.Services.AddSingleton<Domain.IClock, Infrastructure.SystemClock>();
builder.Services.AddScoped<IEmailWorkflowStarter>(sp =>
{
    var client = sp.GetRequiredService<ITemporalClient>();
    var settings = sp.GetRequiredService<AppSettings>();
    return new EmailWorkflowStarter(client, settings.Temporal.TaskQueue);
});
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<Infrastructure.Identity.EmailSettings>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    return new Infrastructure.Identity.EmailSettings
    {
        SmtpHost = settings.Email.SmtpHost,
        SmtpPort = settings.Email.SmtpPort,
        SmtpUser = settings.Email.SmtpUser,
        SmtpPass = settings.Email.SmtpPass,
        FromEmail = settings.Email.FromEmail,
        FromName = settings.Email.FromName,
        BaseUrl = settings.Email.BaseUrl,
        RequireTls = !builder.Environment.IsDevelopment(),
    };
});
builder.Services.AddSingleton<JwtAccessTokenOptions>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    return new JwtAccessTokenOptions
    {
        Issuer = settings.Jwt.Issuer,
        Audience = settings.Jwt.Audience,
        SigningKey = settings.Jwt.SigningKey,
        AccessTokenMinutes = settings.Jwt.AccessTokenMinutes,
    };
});
builder.Services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddScoped<IRecipeCollectionRepository, RecipeCollectionRepository>();
builder.Services.AddScoped<IRecipeFavoriteService, RecipeFavoriteService>();

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    var client = new OpenAIClient(settings.OpenAi.ApiKey);
    var embeddingClient = client.GetEmbeddingClient(RecipeEmbeddingModel.TextEmbedding3Small.OpenAiModelId());
    return embeddingClient.AsIEmbeddingGenerator(RecipeEmbeddingModel.TextEmbedding3Small.Dimensions());
});

builder.Services.AddHttpClient<ICrawlerClient, CrawlerClient>();
builder.Services.AddSingleton<IRecipeExtractor, JsonLdRecipeExtractor>();
builder.Services.AddSingleton<IScraperService, ScraperService>();
builder.Services.AddSingleton<ICrawlerService>(sp => new CrawlerService(
    sp.GetRequiredService<ILogger<CrawlerService>>(),
    sp.GetRequiredService<ITemporalClient>(),
    appSettings.Temporal.TaskQueue
));

builder
    .Services.AddHostedTemporalWorker(appSettings.Temporal.TaskQueue)
    .AddTransientActivities<CrawlerActivities>()
    .AddTransientActivities<RecipeEmbeddingActivities>()
    .AddTransientActivities<EmailActivities>()
    .AddTransientActivities<TokenCleanupActivities>()
    .AddWorkflow<CrawlerWorkflow>()
    .AddWorkflow<EmailVerificationWorkflow>()
    .AddWorkflow<PasswordResetWorkflow>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RecipesDbContext>().Database.MigrateAsync();

    var keyFiles = Directory.Exists(dataProtectionKeyPath)
        ? Directory.GetFiles(dataProtectionKeyPath, "*.xml")
        : Array.Empty<string>();
    if (keyFiles.Length == 0)
    {
        var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
        startupLogger.LogWarning(
            "Data Protection key directory at '{KeyPath}' is empty or missing. "
                + "TOTP secrets encrypted with prior keys will be undecryptable, locking out any 2FA-enabled users. "
                + "Restore the key ring from backup or ensure persistent storage is mounted at this path.",
            dataProtectionKeyPath
        );
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}/openapi.{yaml|json}");
    app.MapScalarApiReference(
        "/docs/v1",
        options => options.WithOpenApiRoutePattern("/openapi/{documentName}/openapi.yaml")
    );
}

app.UseForwardedHeaders();
app.UseHttpLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/ready");
app.MapControllers();

app.Run();

static bool IsKnownNonProductionJwtSigningKey(string signingKey) =>
    signingKey is "replace-with-at-least-32-byte-secret"
        or "development-only-jwt-signing-key-32-bytes-minimum";

public partial class Program;
