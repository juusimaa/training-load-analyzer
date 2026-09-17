using TrainingLoadAnalyzer.Domain;
using TrainingLoadAnalyzer.Infrastructure.Strava;
using TrainingLoadAnalyzer.Infrastructure.Sync;

namespace TrainingLoadAnalyzer.Infrastructure.Tests;

/// <summary>User Story 2 — turning Strava's activities into the analyzer's sessions.</summary>
public sealed class ActivityMappingTests
{
    /// <summary>The fixture summaries from tasks.md. Purpose-built and anonymised (FR-043).</summary>
    private static StravaActivitySummary Summary(
        string id = "11000000001",
        string sportType = "Run",
        string startDate = "2026-09-10T04:30:00Z",
        int utcOffset = 10800,
        int movingTime = 3120,
        int elapsedTime = 4080,
        bool hasHeartrate = true,
        bool manual = false,
        bool isPrivate = false,
        bool trainer = false) => new()
        {
            Id = id,
            SportType = sportType,
            StartDate = DateTimeOffset.Parse(startDate, System.Globalization.CultureInfo.InvariantCulture),
            UtcOffset = utcOffset,
            MovingTime = movingTime,
            ElapsedTime = elapsedTime,
            HasHeartrate = hasHeartrate,
            Manual = manual,
            Private = isPrivate,
            Trainer = trainer,
        };

    // T055: FR-009, FR-010, scenario US2.11.
    [Theory]
    [InlineData("Run", ActivityType.Running)]
    [InlineData("TrailRun", ActivityType.Running)]
    [InlineData("VirtualRun", ActivityType.Running)]
    [InlineData("Ride", ActivityType.Cycling)]
    [InlineData("GravelRide", ActivityType.Cycling)]
    [InlineData("MountainBikeRide", ActivityType.Cycling)]
    [InlineData("VirtualRide", ActivityType.Cycling)]
    public void EverySportTypeInScopeMapsToItsActivityType(string sportType, ActivityType expected)
    {
        var mapped = StravaActivityMapper.Map(Summary(sportType: sportType), heartRate: null);

        Assert.Equal(expected, Assert.IsType<MappedActivity>(mapped).Activity.Type);
    }

    // T057: scenarios US2.1 and US2.10 — everything else is skipped, not forced into a type.
    // T059: the discriminating check. EMountainBikeRide is the sport type most easily forgotten,
    // and a rule that pattern-matched on names containing "MountainBike" would wrongly accept it.
    [Theory]
    [InlineData("Swim")]
    [InlineData("EBikeRide")]
    [InlineData("EMountainBikeRide")]
    [InlineData("Hike")]
    [InlineData("WeightTraining")]
    [InlineData("Handcycle")]
    [InlineData("Velomobile")]
    [InlineData("SomeSportStravaAddsLater")]
    public void EverySportTypeOutOfScopeIsSkippedWithItsReason(string sportType)
    {
        var mapped = StravaActivityMapper.Map(Summary(id: "id-x", sportType: sportType), heartRate: null);

        var skipped = Assert.IsType<SkippedActivity>(mapped);
        Assert.Equal(SkipReason.SportOutOfScope, skipped.Reason);
        Assert.Equal("id-x", skipped.ExternalId);
    }

    // T060: FR-010b — Strava has no treadmill sport type, so indoor training needs no special case.
    [Fact]
    public void AnIndoorRunIsARunLikeAnyOther()
    {
        var mapped = StravaActivityMapper.Map(Summary(trainer: true), heartRate: null);

        Assert.Equal(ActivityType.Running, Assert.IsType<MappedActivity>(mapped).Activity.Type);
    }

    // T061: FR-012, FR-013 — visibility is not a statement about whether the training happened,
    // and a session the athlete typed in is still training.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PrivateAndManualActivitiesAreImportedLikeAnyOther(bool isPrivate, bool manual)
    {
        var mapped = StravaActivityMapper.Map(Summary(isPrivate: isPrivate, manual: manual), heartRate: null);

        Assert.IsType<MappedActivity>(mapped);
    }

    // T062: scenario US2.3, FR-015 — the instant and the athlete's offset, so the session falls on
    // the local day they trained on.
    [Fact]
    public void TheStartCarriesTheAthletesOffsetFromUtc()
    {
        var mapped = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), heartRate: null));

        Assert.True(
            new DateTimeOffset(2026, 9, 10, 7, 30, 0, TimeSpan.FromHours(3))
                .EqualsExact(mapped.Activity.StartedAt));
        Assert.Equal(new DateOnly(2026, 9, 10), DateOnly.FromDateTime(mapped.Activity.StartedAt.Date));
    }

    // T064: scenario US2.4, FR-016 — moving time, never elapsed time.
    [Fact]
    public void MovingTimeIsUsedAndElapsedTimeIsNot()
    {
        var mapped = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), heartRate: null));

        Assert.Equal(TimeSpan.FromMinutes(52), mapped.Activity.MovingTime);
        Assert.NotEqual(TimeSpan.FromMinutes(68), mapped.Activity.MovingTime);
    }

    // T066: FR-014 — the identifier is stored verbatim.
    [Fact]
    public void TheExternalIdentifierIsCarriedVerbatim()
    {
        var mapped = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), heartRate: null));

        Assert.Equal("11000000001", mapped.Activity.ExternalId);
    }

    // T067: scenario US2.5, FR-011 — A7 has no moving time, so the domain refuses it. That refusal
    // is a fact about one activity, not about the import.
    [Fact]
    public void AnActivityTheDomainRefusesIsSkippedWithItsReason()
    {
        var mapped = StravaActivityMapper.Map(
            Summary(id: "11000000007", movingTime: 0, hasHeartrate: false),
            heartRate: null);

        var skipped = Assert.IsType<SkippedActivity>(mapped);
        Assert.Equal(SkipReason.UnusableByDomain, skipped.Reason);
        Assert.Equal("11000000007", skipped.ExternalId);
    }

    // T069: FR-019 — mapping is a pure function of its inputs.
    [Fact]
    public void MappingTheSameSummaryTwiceProducesTheSameSession()
    {
        var first = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), heartRate: null));
        var second = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), heartRate: null));

        Assert.Equal(first.Activity.ExternalId, second.Activity.ExternalId);
        Assert.True(first.Activity.StartedAt.EqualsExact(second.Activity.StartedAt));
        Assert.Equal(first.Activity.MovingTime, second.Activity.MovingTime);
        Assert.Equal(first.Activity.Type, second.Activity.Type);
    }

    // T074: scenario US2.12, FR-017f, FR-017g — THE test that keeps the measured window worth
    // having. Stream S1 opens with two implausible samples, as a real chest strap does before it
    // reads. Without the discarding rule HeartRateSeries refuses the whole series, the session
    // falls to estimated load, and the 180-day window spends a request per activity to achieve
    // nothing (research R21).
    [Fact]
    public void ImplausibleSamplesAreDiscardedAndTheRestOfTheSeriesIsKept()
    {
        var streams = new StravaStreamSet
        {
            Time = new StravaStream { Data = [0, 60, 120, 180] },
            Heartrate = new StravaStream { Data = [0, 0, 142, 150] },
        };

        var mapped = Assert.IsType<MappedActivity>(
            StravaActivityMapper.Map(Summary(), StravaActivityMapper.ToSeries(streams, out var discarded)));

        Assert.Equal(2, discarded);
        Assert.Equal(2, mapped.Activity.HeartRate!.Samples.Count);
        Assert.Equal(142, mapped.Activity.HeartRate.Samples[0].Bpm);
        Assert.Equal(TimeSpan.FromSeconds(120), mapped.Activity.HeartRate.Samples[0].TimeFromStart);

        // The point of all of it: this session carries measured load, not estimated.
        Assert.Equal(LoadProvenance.Measured, mapped.Activity.CalculateTrainingLoad(190).Provenance);
    }

    // T076: scenario US2.13, FR-017f's floor. Two is the fewest samples from which any load can be
    // computed, so no threshold beyond it is invented.
    [Fact]
    public void ASeriesWithFewerThanTwoSurvivingSamplesIsUnusable()
    {
        var streams = new StravaStreamSet
        {
            Time = new StravaStream { Data = [0, 60, 120] },
            Heartrate = new StravaStream { Data = [0, 0, 0] },
        };

        var series = StravaActivityMapper.ToSeries(streams, out var discarded);

        Assert.Null(series);
        Assert.Equal(3, discarded);

        var mapped = Assert.IsType<MappedActivity>(StravaActivityMapper.Map(Summary(), series));
        Assert.Equal(LoadProvenance.Estimated, mapped.Activity.CalculateTrainingLoad(190).Provenance);
    }

    // T077: stream fixture S4, research R20 — a missing stream is an ABSENT KEY, not a null. This
    // must be key-checked rather than indexed.
    [Fact]
    public void AResponseWithNoHeartrateStreamYieldsNoSeriesAndNoError()
    {
        var streams = new StravaStreamSet { Time = new StravaStream { Data = [0, 60] } };

        Assert.Null(StravaActivityMapper.ToSeries(streams, out var discarded));
        Assert.Equal(0, discarded);
    }

    // Stream fixture S3 — nothing implausible, nothing discarded.
    [Fact]
    public void ACleanSeriesLosesNothing()
    {
        var streams = new StravaStreamSet
        {
            Time = new StravaStream { Data = [0, 300, 600] },
            Heartrate = new StravaStream { Data = [95, 142, 171] },
        };

        var series = StravaActivityMapper.ToSeries(streams, out var discarded);

        Assert.Equal(0, discarded);
        Assert.Equal(3, series!.Samples.Count);
    }
}
