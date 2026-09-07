using EventService.Application.Abstractions;
using EventService.Infrastructure.Caching;
using EventService.Infrastructure.Messaging;
using EventService.Infrastructure.Persistence;
using EventService.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EventService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEventServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<EventDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "events");
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            });
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration["Redis:Connection"] ?? "localhost:6379"));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventListCache, EventListCache>();

        // Startup schema migration + one-shot reference seed (feature 003).
        var seed = configuration.GetSection(SeedPathOptions.SectionName).Get<SeedPathOptions>()
            ?? new SeedPathOptions();
        services.AddSingleton(seed);
        services.AddScoped<EventDatabaseSeeder>();

        // Direct AWS SNS/SQS messaging (US1).
        var messaging = configuration.GetSection(AwsMessagingOptions.SectionName)
            .Get<AwsMessagingOptions>() ?? new AwsMessagingOptions();
        var aws = configuration.GetSection(AwsCredentialsOptions.SectionName)
            .Get<AwsCredentialsOptions>() ?? new AwsCredentialsOptions();
        services.AddSingleton(messaging);
        services.AddSingleton(aws);
        services.AddSingleton(AwsClients.CreateSns(aws));
        services.AddSingleton(AwsClients.CreateSqs(aws));
        services.AddSingleton<AwsBrokerState>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddHostedService<BrokerProvisioner>();
        services.AddHostedService<OutboxRelay>();

        return services;
    }
}