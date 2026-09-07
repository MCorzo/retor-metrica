using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.SeedData;

public sealed class NotificationDatabaseSeeder
{
    private const string MarkerTableSql =
        """
        CREATE TABLE IF NOT EXISTS notifications.seeds_applied (
            id          uuid PRIMARY KEY,
            applied_at  timestamptz NOT NULL
        );
        """;

private const string MarkerSelectSql =
        """
        SELECT COUNT(*) AS "Value" FROM notifications.seeds_applied
        """;

    private const string MarkerInsertSql =
        """
        INSERT INTO notifications.seeds_applied (id, applied_at)
        VALUES ({0}, {1})
        ON CONFLICT (id) DO NOTHING
        """;

    private readonly NotificationDbContext _context;
    private readonly SeedPathOptions _options;
    private readonly ILogger<NotificationDatabaseSeeder> _logger;

    public NotificationDatabaseSeeder(
        NotificationDbContext context,
        SeedPathOptions options,
        ILogger<NotificationDatabaseSeeder> logger)
    {
        _context = context;
        _options = options;
        _logger = logger;
    }

    public async Task EnsureDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Path))
        {
            _logger.LogInformation("Seed:Path is not configured; skipping schema/seed startup step.");
            return;
        }

        var seedDir = new DirectoryInfo(_options.Path);
        if (!seedDir.Exists)
        {
            _logger.LogWarning("Seed folder '{SeedPath}' does not exist; skipping seed startup step.", _options.Path);
            return;
        }

        _logger.LogInformation("Applying pending EF migrations for the notifications schema.");
        await _context.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Ensuring seed marker table exists (notifications.seeds_applied).");
        await _context.Database.ExecuteSqlRawAsync(MarkerTableSql, cancellationToken);

        var applied = await _context.Database
            .SqlQueryRaw<long>(MarkerSelectSql)
            .SingleAsync(cancellationToken);

        if (applied > 0)
        {
            _logger.LogInformation("Seed already applied (notifications.seeds_applied present); nothing to do.");
            return;
        }

        var files = seedDir.EnumerateFiles("*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file.FullName, cancellationToken);
            _logger.LogInformation("Executing seed script '{SeedFile}' ({FilePath}).", file.Name, file.FullName);
            await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        await _context.Database.ExecuteSqlRawAsync(
            MarkerInsertSql,
            [Guid.CreateVersion7(), DateTimeOffset.UtcNow],
            cancellationToken);

        _logger.LogInformation("Seed completed for notifications schema ({Count} script(s) applied).", files.Count);
    }
}
