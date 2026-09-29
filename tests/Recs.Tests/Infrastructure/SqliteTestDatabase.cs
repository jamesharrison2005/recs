using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recs.Infrastructure.Persistence;

namespace Recs.Tests.Infrastructure;

/// <summary>
/// Creates a throwaway SQLite in-memory database with the Recs schema applied.
/// The connection must stay open for the lifetime of the database, so callers own
/// the instance and dispose it.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RecsDbContext> _options;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<RecsDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new RecsDbContext(_options);
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a brand new context over the same database, so reads are served
    /// from SQLite rather than from the change tracker of the writing context.
    /// </summary>
    public RecsDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
