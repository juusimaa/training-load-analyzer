using Microsoft.EntityFrameworkCore;
using TrainingLoadAnalyzer.Infrastructure.Persistence;
using TrainingLoadAnalyzer.Infrastructure.Tests.Fakes;

namespace TrainingLoadAnalyzer.Web.Tests.Fakes;

/// <summary>
///   A context factory that does not answer until the test lets it.
/// </summary>
/// <remarks>
///   <para>
///     The only way to observe the dashboard's loading state. <c>DashboardReader</c> is a concrete
///     class the page injects directly, so there is nothing to substitute at that level — but the
///     reader awaits <see cref="IDbContextFactory{TContext}.CreateDbContextAsync"/>, and holding
///     that open leaves the page's first render exactly where an athlete's first paint finds it.
///   </para>
///   <para>
///     A gate rather than a delay. A test that waits a fixed number of milliseconds for a state to
///     appear is a test that passes on a fast machine and fails in CI.
///   </para>
/// </remarks>
internal sealed class PendingContextFactory(SqliteFixture<ImportDbContext> fixture)
    : IDbContextFactory<ImportDbContext>
{
    private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ImportDbContext CreateDbContext() => fixture.NewContext();

    public async Task<ImportDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        await gate.Task.WaitAsync(cancellationToken);

        return fixture.NewContext();
    }

    /// <summary>Lets the read complete, so the page can render what it found.</summary>
    public void Release() => gate.TrySetResult();
}
