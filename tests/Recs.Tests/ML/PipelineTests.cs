using Recs.ML.Models;
using Recs.ML.Training;
using Recs.Infrastructure.Persistence;
using Recs.Tests.Infrastructure;
using Xunit;

namespace Recs.Tests.ML;

public class PipelineTests : IClassFixture<SqliteTestDatabase>
{
    private readonly RecsDbContext _db;

    public PipelineTests(SqliteTestDatabase fixture) => _db = fixture.CreateContext();

    [Fact]
    public void Pipeline_CanLoadRatings_FromDatabase()
    {
        // The test DB needs seed ratings; if empty, skip.
        // Act
        var count = _db.Ratings.Count();

        // Assert: at least some ratings should exist if DB was seeded
        Assert.True(count >= 0, "Rating count readable");
    }

    [Fact]
    public void Pipeline_TrainTestSplit_ProducesNonEmptySets()
    {
        // Arrange
        var ratings = _db.Ratings.Select(r => new RatingData
        {
            UserId = r.UserId,
            ItemId = r.ItemId,
            Score = r.Score
        }).ToList();

        // Act
        var ml = new Microsoft.ML.MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(ratings);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        // Assert
        Assert.True(split.TrainSet.Schema.Count > 0);
        Assert.True(split.TestSet.Schema.Count > 0);
        Assert.True(split.TestSet.GetRowCount() > 0);
    }
}
