using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventService.Infrastructure.SeedData;

public sealed class EventDatabaseSeeder
{
    private const string MarkerTableSql =
        """
        CREATE TABLE IF NOT EXISTS events.seeds_applied (
            id          uuid PRIMARY KEY,
            applied_at  timestamptz NOT NULL
        );
        """;

private const string MarkerSelectSql =
        """
        SELECT COUNT(*) AS "Value" FROM events.seeds_applied
        """;

    private const string MarkerInsertSql =
        """
        INSERT INTO events.seeds_applied (id, applied_at)
        VALUES ({0}, {1})
        ON CONFLICT (id) DO NOTHING
        """;

    private readonly EventDbContext _context;
    private readonly SeedPathOptions _options;
    private readonly ILogger<EventDatabaseSeeder> _logger;

    public EventDatabaseSeeder(
        EventDbContext context,
        SeedPathOptions options,
        ILogger<EventDatabaseSeeder> logger)
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

        _logger.LogInformation("Applying pending EF migrations for the events schema.");
        await _context.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Ensuring seed marker table exists (events.seeds_applied).");
        await _context.Database.ExecuteSqlRawAsync(MarkerTableSql, cancellationToken);

        var applied = await _context.Database
            .SqlQueryRaw<long>(MarkerSelectSql)
            .SingleAsync(cancellationToken);

        if (applied > 0)
        {
            _logger.LogInformation("Seed already applied (events.seeds_applied present); nothing to do.");
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

        _logger.LogInformation("Seed completed for events schema ({Count} script(s) applied).", files.Count);
    }
}
