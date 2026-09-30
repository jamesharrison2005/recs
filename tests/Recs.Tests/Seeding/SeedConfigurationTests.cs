using Recs.Seeder;

namespace Recs.Tests.Seeding;

public class SeedConfigurationTests
{
    private static readonly string[] SampleArgs =
    [
        "--businesses", "data/business.json",
        "--reviews", "data/review.json",
        "--city", "Nashville",
        "--state", "TN",
        "--max-businesses", "25",
        "--max-users", "100",
        "--max-reviews", "500",
        "--min-ratings-per-user", "3",
        "--min-ratings-per-item", "2",
        "--batch-size", "250"
    ];

    [Fact]
    public void Build_MapsEveryDocumentedFlagOntoItsSeedOption()
    {
        var configuration = SeedConfiguration.Build(SampleArgs);

        var options = SeedOptions.FromConfiguration(configuration);

        Assert.Equal("data/business.json", options.BusinessFilePath);
        Assert.Equal("data/review.json", options.ReviewFilePath);
        Assert.Equal("Nashville", options.City);
        Assert.Equal("TN", options.State);
        Assert.Equal(25, options.MaxBusinesses);
        Assert.Equal(100, options.MaxUsers);
        Assert.Equal(500, options.MaxReviews);
        Assert.Equal(3, options.MinRatingsPerUser);
        Assert.Equal(2, options.MinRatingsPerItem);
        Assert.Equal(250, options.BatchSize);
        Assert.Null(options.Validate());
    }

    [Fact]
    public void Build_OverridesFileAndEnvironmentValues()
    {
        var configuration = SeedConfiguration.Build(SampleArgs);

        var section = configuration.GetSection(SeedOptions.SectionName);

        Assert.Equal("data/business.json", section[nameof(SeedOptions.BusinessFilePath)]);
        Assert.Equal("data/review.json", section[nameof(SeedOptions.ReviewFilePath)]);
        Assert.Equal("Nashville", section[nameof(SeedOptions.City)]);
        Assert.Equal("TN", section[nameof(SeedOptions.State)]);
        Assert.Equal("25", section[nameof(SeedOptions.MaxBusinesses)]);
        Assert.Equal("100", section[nameof(SeedOptions.MaxUsers)]);
        Assert.Equal("500", section[nameof(SeedOptions.MaxReviews)]);
        Assert.Equal("3", section[nameof(SeedOptions.MinRatingsPerUser)]);
        Assert.Equal("2", section[nameof(SeedOptions.MinRatingsPerItem)]);
        Assert.Equal("250", section[nameof(SeedOptions.BatchSize)]);
    }

    [Fact]
    public void Build_WithoutFileFlags_LeavesOptionsInvalid()
    {
        // The regression this guards: unmapped flags are silently dropped, so both file paths
        // stay empty and validation rejects the run.
        var configuration = SeedConfiguration.Build(["--city", "Nashville", "--state", "TN"]);

        var options = SeedOptions.FromConfiguration(configuration);

        Assert.Equal(string.Empty, options.BusinessFilePath);
        Assert.Equal(string.Empty, options.ReviewFilePath);
        Assert.Equal("Nashville", options.City);
        Assert.Contains("Business file path is required", options.Validate());
    }

    [Fact]
    public void SwitchMappings_CoverEveryFlagPrintedByTheUsageText()
    {
        string[] documentedFlags =
        [
            "--businesses", "--reviews", "--city", "--state", "--max-businesses", "--max-users",
            "--max-reviews", "--min-ratings-per-user", "--min-ratings-per-item", "--batch-size"
        ];

        Assert.Equal(documentedFlags.Length, SeedConfiguration.SwitchMappings.Count);

        foreach (var flag in documentedFlags)
        {
            Assert.True(
                SeedConfiguration.SwitchMappings.TryGetValue(flag, out var key),
                $"Usage advertises {flag} but it has no switch mapping.");
            Assert.StartsWith($"{SeedOptions.SectionName}:", key);
        }
    }
}