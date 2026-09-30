using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recs.Infrastructure.Persistence;

namespace Recs.Tests.Seeding;

/// <summary>
/// A throwaway SQLite in-memory database with the Recs schema created by running the real
/// migrations, rather than <c>EnsureCreated</c> as <see cref="SqliteTestDatabase"/> does.
/// <para>
/// The seeder calls <c>Migrate</c> itself, and EF refuses to migrate a database that
/// <c>EnsureCreated</c> already built, so the seeder tests need this variant to exercise the
/// same startup path production uses.
/// </para>
/// </summary>
public sealed class SqliteMigratedTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RecsDbContext> _options;

    public SqliteMigratedTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<RecsDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new RecsDbContext(_options);
        context.Database.Migrate();
    }

    /// <summary>
    /// Creates a new context over the same database, so reads are served from SQLite rather than
    /// from the change tracker of the writing context.
    /// </summary>
    public RecsDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
