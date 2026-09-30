using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recs.Infrastructure;
using Recs.Infrastructure.Persistence;
using Recs.Seeder;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

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
    var connectionString = context.Database.GetConnectionString();
    Console.WriteLine($"Database: {Path.GetFullPath(connectionString ?? "(unset)")}");

    var loader = new SeedDataLoader(context, options, Console.WriteLine);
    var summary = await loader.RunAsync();

    Console.WriteLine(summary.Render());

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seed failed: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project src/Recs.Seeder -- --businesses <path> --reviews <path> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options (all optional except the two file paths, and overridable in");
    Console.Error.WriteLine("appsettings.json under the \"Seed\" section):");
    Console.Error.WriteLine("  --city <name>                    default: Nashville, TN");
    Console.Error.WriteLine("  --state <code>                   optional second location filter");
    Console.Error.WriteLine("  --max-businesses <n>             default: 5000");
    Console.Error.WriteLine("  --max-users <n>                  default: 20000");
    Console.Error.WriteLine("  --max-reviews <n>                default: 200000");
    Console.Error.WriteLine("  --min-ratings-per-user <n>       default: 5");
    Console.Error.WriteLine("  --min-ratings-per-item <n>       default: 5");
    Console.Error.WriteLine("  --batch-size <n>                 default: 1000");
    Console.Error.WriteLine("  --ConnectionStrings:DefaultConnection \"Data Source=<abs path>\"");
}
