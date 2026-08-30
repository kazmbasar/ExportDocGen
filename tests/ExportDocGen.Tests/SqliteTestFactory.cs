using ExportDocGen.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Tests;

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> backed by a single in-memory
/// SQLite connection kept open for the lifetime of the test, so relational
/// behaviour (FKs, cascade delete, unique indexes) matches production.
/// </summary>
public sealed class SqliteTestFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteTestFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
