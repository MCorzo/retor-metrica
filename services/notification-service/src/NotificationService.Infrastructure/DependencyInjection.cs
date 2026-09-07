using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;
using NotificationService.Infrastructure.Mail;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.SeedData;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<NotificationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "notifications");
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            });
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Startup schema migration.
        var seed = configuration.GetSection(SeedPathOptions.SectionName).Get<SeedPathOptions>()
            ?? new SeedPathOptions();
        services.AddSingleton(seed);
        services.AddScoped<NotificationDatabaseSeeder>();

        var smtp = configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
        services.AddSingleton(smtp);
        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(email);
        services.AddSingleton<IAuditEmailTarget>(new AdminEmailTarget(
            configuration["Email:AdminAddress"] ?? "admin@event-platform.local"));
        services.AddScoped<EventEmailBuilder>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailScanner>();

        // Direct AWS SNS/SQS messaging.
        var messaging = configuration.GetSection(AwsMessagingOptions.SectionName)
            .Get<AwsMessagingOptions>() ?? new AwsMessagingOptions();
        var aws = configuration.GetSection(AwsCredentialsOptions.SectionName)
            .Get<AwsCredentialsOptions>() ?? new AwsCredentialsOptions();
        services.AddSingleton(messaging);
        services.AddSingleton(aws);
        services.AddSingleton(AwsClients.CreateSns(aws));
        services.AddSingleton(AwsClients.CreateSqs(aws));
        services.AddSingleton<AwsBrokerState>();
        services.AddSingleton<DeadLetterRelay>();
        services.AddScoped<EventCreatedProcessor>();
        services.AddHostedService<BrokerProvisioner>();
        services.AddHostedService<SqsConsumerHostedService>();

        return services;
    }
}
