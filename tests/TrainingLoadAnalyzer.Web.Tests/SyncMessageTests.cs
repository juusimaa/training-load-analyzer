using TrainingLoadAnalyzer.Infrastructure.Sync;
using TrainingLoadAnalyzer.Web.Features.Sync;

namespace TrainingLoadAnalyzer.Web.Tests;

/// <summary>
///   FR-010 and all five of US5's scenarios, as pure assertions — no rendering, no Strava, no
///   database. That is research R11's shape paying off: the question "what does the page say?" has
///   a right answer, so it does not need a renderer to ask it.
/// </summary>
public class SyncMessageTests
{
    private static SyncStatus Finished(SyncOutcome outcome, int imported = 0, DateTimeOffset? retryAfter = null) =>
        new()
        {
            Result = new SyncResult { Outcome = outcome, Imported = imported, RetryAfter = retryAfter },
            FinishedAt = new DateTimeOffset(2026, 9, 18, 7, 0, 0, TimeSpan.FromHours(3)),
            RetryAfterLocal = retryAfter,
        };

    /// <summary>US5 scenario 1.</summary>
    [Fact]
    public void A_running_sync_says_so()
        => Assert.Contains("Syncing", SyncMessage.For(new SyncStatus { IsRunning = true }), StringComparison.Ordinal);

    /// <summary>US5 scenario 2: the count is the point — "done" alone answers nothing.</summary>
    [Fact]
    public void A_completed_sync_reports_how_many_activities_arrived()
    {
        var message = SyncMessage.For(Finished(SyncOutcome.Completed, imported: 3));

        Assert.Contains("3", message, StringComparison.Ordinal);
        Assert.Contains("imported", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>US5 scenario 3: nothing new is a good outcome and must not read as a failure.</summary>
    [Fact]
    public void A_sync_that_found_nothing_says_the_history_is_current()
        => Assert.Contains(
            "Already up to date",
            SyncMessage.For(Finished(SyncOutcome.Completed)),
            StringComparison.Ordinal);

    /// <summary>
    ///   US5 scenario 4. Feature 005 reports Strava's own next window boundary rather than a fixed
    ///   delay (005 FR-035), so the athlete is told when to come back, not merely to wait.
    /// </summary>
    [Fact]
    public void A_rate_limited_sync_says_when_it_can_be_retried()
    {
        var retryAt = new DateTimeOffset(2026, 9, 18, 7, 15, 0, TimeSpan.FromHours(3));
        var message = SyncMessage.For(Finished(SyncOutcome.RateLimited, retryAfter: retryAt));

        Assert.Contains("Rate limited", message, StringComparison.Ordinal);
        // 07:15 because the value carries a +03:00 offset, not because this machine happens to sit
        // in that zone. An implementation calling ToLocalTime() reads 04:15 on a UTC build agent.
        Assert.Contains("07:15", message, StringComparison.Ordinal);
    }

    /// <summary>US5 scenario 4's other half: an interruption is recoverable and says so.</summary>
    [Fact]
    public void An_interrupted_sync_invites_another_attempt()
        => Assert.Contains(
            "interrupted",
            SyncMessage.For(Finished(SyncOutcome.Interrupted)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>US5 scenario 5 and FR-018, from both directions: a rejected credential, and no account at all.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_missing_or_rejected_connection_asks_the_athlete_to_connect(bool rejected)
    {
        var status = rejected
            ? Finished(SyncOutcome.ReconnectionRequired)
            : new SyncStatus { Failure = "No Strava account is connected." };

        Assert.Contains("Strava connection required", SyncMessage.For(status), StringComparison.Ordinal);
    }

    /// <summary>005 FR-040: a second sync is refused rather than queued, and the athlete is told why.</summary>
    [Fact]
    public void A_refused_sync_says_one_is_already_running()
        => Assert.Contains(
            "already running",
            SyncMessage.For(Finished(SyncOutcome.Refused)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Nothing has happened yet, so there is nothing to report.</summary>
    [Fact]
    public void A_sync_that_has_never_run_says_nothing_alarming()
        => Assert.Equal(string.Empty, SyncMessage.For(SyncStatus.Never));

    /// <summary>
    ///   C97: total. Every <c>SyncOutcome</c> — including one added to feature 005 later — yields a
    ///   message rather than an empty string or an exception. A sync that ends in a way the page
    ///   cannot describe is Principle VI's silent failure wearing a different hat.
    /// </summary>
    [Fact]
    public void Every_outcome_has_something_to_say()
    {
        foreach (var outcome in Enum.GetValues<SyncOutcome>())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(SyncMessage.For(Finished(outcome))),
                $"{outcome} has no message.");
        }
    }

    /// <summary>C97 and 005 C66: no message can carry a credential.</summary>
    [Fact]
    public void No_message_carries_a_credential()
    {
        foreach (var outcome in Enum.GetValues<SyncOutcome>())
        {
            var message = SyncMessage.For(Finished(outcome, imported: 2));

            Assert.DoesNotContain("token", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
