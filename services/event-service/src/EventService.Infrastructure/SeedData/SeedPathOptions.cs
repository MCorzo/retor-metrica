namespace EventService.Infrastructure.SeedData;

/// <summary>
/// Binds the <c>Seed</c> configuration section. <c>Path</c> points at the folder
/// baked into the image with the versioned <c>*.sql</c> seed scripts. Container
/// default is <c>/app/seed</c> (Dockerfile COPY); local <c>dotnet run</c>
/// overrides it via <c>Seed__Path</c> to point at a local <c>db/&lt;service&gt;</c>
/// folder, or skips seeding when the folder is absent.
/// </summary>
public sealed class SeedPathOptions
{
    public const string SectionName = "Seed";

    public string Path { get; set; } = string.Empty;
}