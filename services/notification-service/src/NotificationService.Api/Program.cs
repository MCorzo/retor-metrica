using System.Threading.RateLimiting;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Api.Auth;
using NotificationService.Api.Health;
using NotificationService.Api.Logging;
using NotificationService.Api.Middleware;
using NotificationService.Api.OpenApi;
using NotificationService.Application;
using NotificationService.Application.Abstractions;
using NotificationService.Infrastructure;
using NotificationService.Infrastructure.SeedData;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.AwsCloudWatch;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

    var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
    loggerConfiguration.Enrich.With(new CorrelationIdEnricher(httpContextAccessor));

    // Never serialize AWS credentials into logs (US2).
    var secrets = new[] { context.Configuration["Aws:AccessKey"], context.Configuration["Aws:SecretKey"] }
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s!)
        .ToArray();
    if (secrets.Length > 0)
    {
        loggerConfiguration.Enrich.With(new SecretRedactionEnricher(secrets));
    }

    if (context.Configuration.GetSection("CloudWatch:Enabled").Get<bool>())
    {
        var logGroup = context.Configuration["CloudWatch:LogGroup"] ?? "event-platform";
        var region = context.Configuration["Aws:Region"] ?? "us-east-1";
        var logStreamProvider = new DefaultLogStreamProvider();

        var outputTem = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz } {RequestId,13} [{Level:u3}] {Message:lj} {Properties} {NewLine}{Exception}";
        var formatter = new MessageTemplateTextFormatter(outputTem);

        loggerConfiguration.WriteTo.AmazonCloudWatch(
            new CloudWatchSinkOptions
            {
                LogGroupName = logGroup,
                LogStreamNameProvider = logStreamProvider,
                Period = TimeSpan.FromSeconds(30),
                CreateLogGroup = true,
                TextFormatter = formatter
            },
            CreateCloudWatchClient(context.Configuration, region));
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<IAuditUserProvider>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://datatracker.ietf.org/doc/html/rfc9457",
            status = 429,
            title = "Too many requests",
        });
    };

    options.AddPolicy("user-or-ip", httpContext =>
    {
        var key = httpContext.User.SubjectId()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

var oidcAuthority = builder.Configuration["Oidc:Authority"];
var oidcAudience = builder.Configuration["Oidc:Audience"] ?? "master-realm";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwt =>
    {
        if (!string.IsNullOrWhiteSpace(oidcAuthority))
        {
            jwt.Authority = oidcAuthority;
            jwt.Audience = oidcAudience;
            jwt.RequireHttpsMetadata = oidcAuthority.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        }

        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(oidcAuthority),
            ValidateAudience = !string.IsNullOrWhiteSpace(oidcAudience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // Override the issuer used for validation when Keycloak is reached
        // through a different URL than the one used to mint tokens (e.g.
        // tokens minted via host "localhost:8080" while the service fetches
        // JWKS via "host.docker.internal:8080"). Defaults to the authority.
        var oidcIssuer = builder.Configuration["Oidc:Issuer"];
        if (!string.IsNullOrWhiteSpace(oidcIssuer))
        {
            jwt.TokenValidationParameters.ValidIssuer = oidcIssuer;
        }
    });

builder.Services.AddAuthorization(ApiPolicies.AddNotificationServiceAuthorization);

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Default") ?? string.Empty,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddNotificationServiceApplication();
builder.Services.AddNotificationServiceInfrastructure(builder.Configuration);
builder.Services.AddHostedService<CloudWatchRetentionService>();

var app = builder.Build();

// Feature 003: create/migrate the service schema and apply one-shot reference
// seed on first start (guarded by Seed:Path; idle on later restarts). Fail-fast:
// any migration/seed error halts startup before the app can serve traffic.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<NotificationDatabaseSeeder>();
    await seeder.EnsureDatabaseAsync();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "NotificationService", docs = "/scalar/v1", health = "/health/live" }));

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = ReadyResponseWriter.Write,
});

app.MapScalarApiReference();
app.Run();

public sealed partial class Program
{
    /// <summary>
    /// Builds the CloudWatch Logs client with the same static credentials used by
    /// the SNS/SQS clients whenever configured; otherwise falls back to the default
    /// credential chain (provisioned role).
    /// </summary>
    private static AmazonCloudWatchLogsClient CreateCloudWatchClient(
        IConfiguration configuration, string region)
    {
        var accessKey = configuration["Aws:AccessKey"];
        var secretKey = configuration["Aws:SecretKey"];
        var endpoint = RegionEndpoint.GetBySystemName(region);

        return !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonCloudWatchLogsClient(new BasicAWSCredentials(accessKey, secretKey), endpoint)
            : new AmazonCloudWatchLogsClient(endpoint);
    }
}
