using Microsoft.Extensions.Configuration;

namespace Recs.Seeder;

/// <summary>
/// Builds the seeder's configuration and maps the documented command line flags onto
/// <see cref="SeedOptions"/> keys. Lives here rather than in <c>Program</c> so the switch
/// mappings can be covered by tests.
/// </summary>
public static class SeedConfiguration
{
    /// <summary>
    /// Maps each documented command line flag to the <c>Seed</c> configuration key it sets.
    /// Without these, <see cref="CommandLineConfigurationProvider"/> would treat
    /// <c>--businesses</c> as an unknown switch and the value would never reach the options.
    /// </summary>
    private static readonly Dictionary<string, string> Mappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["--businesses"] = $"{SeedOptions.SectionName}:BusinessFilePath",
            ["--reviews"] = $"{SeedOptions.SectionName}:ReviewFilePath",
            ["--city"] = $"{SeedOptions.SectionName}:City",
            ["--state"] = $"{SeedOptions.SectionName}:State",
            ["--max-businesses"] = $"{SeedOptions.SectionName}:MaxBusinesses",
            ["--max-users"] = $"{SeedOptions.SectionName}:MaxUsers",
            ["--max-reviews"] = $"{SeedOptions.SectionName}:MaxReviews",
            ["--min-ratings-per-user"] = $"{SeedOptions.SectionName}:MinRatingsPerUser",
            ["--min-ratings-per-item"] = $"{SeedOptions.SectionName}:MinRatingsPerItem",
            ["--batch-size"] = $"{SeedOptions.SectionName}:BatchSize"
        };

    /// <summary>The flag to <c>Seed</c> key mappings, exposed so tests can assert coverage.</summary>
    public static IReadOnlyDictionary<string, string> SwitchMappings => Mappings;

    /// <summary>
    /// Builds the configuration from appsettings.json, environment variables and command line
    /// arguments, in ascending order of priority.
    /// </summary>
    public static IConfigurationRoot Build(string[] args, string? basePath = null)
    {
        return new ConfigurationBuilder()
            .SetBasePath(basePath ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args, Mappings)
            .Build();
    }
}