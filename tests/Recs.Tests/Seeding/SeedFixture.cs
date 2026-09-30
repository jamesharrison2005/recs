using Recs.Seeder;

namespace Recs.Tests.Seeding;

/// <summary>
/// Locates the checked-in Yelp sample files. They are copied to the build output by the
/// <c>Content</c> item group in <c>Recs.Tests.csproj</c>, which is required because the base
/// SDK does not copy .json files for a non-web project.
/// </summary>
public static class SeedFixture
{
    public static string BusinessFilePath => Resolve("business.json");

    public static string ReviewFilePath => Resolve("review.json");

    private static string Resolve(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fixture not found at \"{path}\". The Content item group in Recs.Tests.csproj " +
                "is responsible for copying it; check that it still has a build action of Copy.",
                path);
        }

        return path;
    }

    /// <summary>
    /// Options pointed at the sample fixtures, with the density filter relaxed so the small
    /// sample survives it.
    /// </summary>
    public static SeedOptions CreateOptions(Action<SeedOptions>? configure = null)
    {
        var options = new SeedOptions
        {
            BusinessFilePath = BusinessFilePath,
            ReviewFilePath = ReviewFilePath,
            City = "Nashville",
            State = "TN",
            MinRatingsPerUser = 1,
            MinRatingsPerItem = 1,
            BatchSize = 2
        };

        configure?.Invoke(options);
        return options;
    }
}
