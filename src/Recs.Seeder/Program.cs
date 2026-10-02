using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recs.Infrastructure;
using Recs.Infrastructure.Persistence;
using Recs.Seeder;

// The loader is built before the run so that its summary is reachable from the catch block. A
// failure part way through a multi-minute run would otherwise leave only an exception message,
// with no record of how far the run actually got.
SeedDataLoader? loader = null;

try
{
    var configuration = SeedConfiguration.Build(args);

    var options = SeedOptions.FromConfiguration(configuration);

    if (options.Validate() is { } problem)
    {
        Console.Error.WriteLine($"Invalid options: {problem}");
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }

    if (!File.Exists(options.BusinessFilePath))
    {
        Console.Error.WriteLine($"Business file not found: {options.BusinessFilePath}");
        return 1;
    }

    if (!File.Exists(options.ReviewFilePath))
    {
        Console.Error.WriteLine($"Review file not found: {options.ReviewFilePath}");
        return 1;
    }

    // Reuses the same DI wiring the API uses, so the seeder and the API resolve an identical
    // DbContext against an identical connection string.
    var services = new ServiceCollection();
    services.AddInfrastructure(configuration);

    await using var provider = services.BuildServiceProvider();

    await using var scope = provider.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<RecsDbContext>();
    Console.WriteLine($"Database: {ResolveDatabaseFile(context)}");

    loader = new SeedDataLoader(context, options, Console.WriteLine);
    var summary = await loader.RunAsync();

    Console.WriteLine(summary.Render());

    return 0;
}
catch (Exception ex)
{
    // The summary comes first: it is what the run managed to do, and it is the part that tells
    // you whether the failure came before or after any data was written.
    if (loader is not null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== Seed run summary (run did not complete) ===");
        Console.Error.WriteLine(loader.Summary.Render());
    }

    // Type and stack as well as the message: a bare message is rarely enough to tell a missing
    // migration from a malformed row from a locked database file.
    Console.Error.WriteLine($"Seed failed: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);

    return 1;
}

/// <summary>
/// The file the context is actually writing to. A relative connection string such as
/// "Data Source=recs.db" is resolved by SQLite against the process working directory, so the
/// connection string is not a path and cannot be printed as one: doing so produces
/// "C:\...\repo Data Source=C:\...\repo\recs.db". The connection's own DataSource is the file, and
/// GetFullPath expands it the same way the provider does.
/// </summary>
static string ResolveDatabaseFile(RecsDbContext context)
{
    if (context.Database.GetDbConnection() is SqliteConnection connection &&
        !string.IsNullOrWhiteSpace(connection.DataSource))
    {
        return Path.GetFullPath(connection.DataSource);
    }

    return context.Database.GetConnectionString() ?? "(unset)";
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project src/Recs.Seeder -- --businesses <path> --reviews <path> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options (all optional except the two file paths, and overridable in");
    Console.Error.WriteLine("appsettings.json under the \"Seed\" section):");
    Console.Error.WriteLine("  --city <name>                    default: Nashville");
    Console.Error.WriteLine("  --state <code>                   optional second location filter");
    Console.Error.WriteLine("  --max-businesses <n>             default: 5000");
    Console.Error.WriteLine("  --max-users <n>                  default: 20000");
    Console.Error.WriteLine("  --max-reviews <n>                default: 200000");
    Console.Error.WriteLine("  --min-ratings-per-user <n>       default: 5");
    Console.Error.WriteLine("  --min-ratings-per-item <n>       default: 5");
    Console.Error.WriteLine("  --batch-size <n>                 default: 1000");
    Console.Error.WriteLine("  --ConnectionStrings:DefaultConnection \"Data Source=<abs path>\"");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Re-running is safe: rows that are already present are counted and left");
    Console.Error.WriteLine("alone rather than inserted again. The exit code is 0 on success and 1");
    Console.Error.WriteLine("on any failure.");
}