using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

/// <summary>
///   Real SQLite, held in memory. Not the EF Core InMemory provider, which was compared side by side
///   and <em>succeeds</em> at two queries that throw on real SQLite — a suite built on it would
///   green-light code that fails in production (research R8).
/// </summary>
/// <remarks>
///   <para>
///     Three traps, each verified by running it. The connection <em>object</em> must be handed to
///     <c>UseSqlite</c>, never the connection string: given a string, EF Core opens and closes a
///     connection per operation, so the schema is created and the database then evaporates
///     (<c>SQLite Error 1: 'no such table'</c>). The connection must stay open for the fixture's
///     lifetime — closing and reopening the same object destroys the data, because pooling does not
///     preserve a <c>:memory:</c> database. And two <c>:memory:</c> connections are two separate
///     databases, which is exactly the isolation wanted: one fixture per test class.
///   </para>
/// </remarks>
internal sealed class SqliteFixture<TContext> : IDisposable
    where TContext : DbContext
{
    private readonly SqliteConnection connection;
    private readonly Func<DbContextOptions<TContext>, TContext> factory;

    public SqliteFixture(Func<DbContextOptions<TContext>, TContext> factory)
    {
        this.factory = factory;

        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = NewContext();
        context.Database.Migrate();
    }

    /// <summary>
    ///   A fresh context over the same database. Reading through one of these after writing through
    ///   another is what proves a value survived storage rather than being served from the change
    ///   tracker (FR-022, FR-024).
    /// </summary>
    public TContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        return factory(options);
    }

    /// <summary>
    ///   Reads raw column values, bypassing EF Core entirely. The assertion the InMemory provider
    ///   cannot make: a domain-level round trip passes against several wrong mappings, and only this
    ///   pins the stored form (research R8).
    /// </summary>
    public object? Scalar(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return command.ExecuteScalar();
    }

    public void Dispose() => connection.Dispose();
}
