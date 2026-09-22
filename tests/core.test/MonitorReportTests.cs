using core.monitor;

namespace core.test;

public class MonitorReportTests
{

    #region Helper Methods

    private static readonly DateTime Yesterday = new(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

    private static TopSnapshot Snapshot(DateTime capturedAt, TopEntityType entityType, params string[] names)
    {
        return new TopSnapshot
        {
            CapturedAt = capturedAt,
            EntityType = entityType,
            TimeRange = "short",
            Entries = names
                .Select((name, index) => new TopEntry(index + 1, $"id-{name}", name, string.Empty))
                .ToList()
        };
    }

    private static MonitorRunResult Result(params TopDiff[] diffs)
    {
        return new MonitorRunResult { CapturedAt = Today, Diffs = diffs };
    }

    #endregion

    #region Tests

    [Test]
    public void BuildToastTitle_OnFirstRun_SaysBaseline()
    {
        var result = Result(TopDiffCalculator.Compare(null, Snapshot(Today, TopEntityType.Artist, "a", "b")));

        Assert.That(MonitorReport.BuildToastTitle(result), Is.EqualTo("Spotify: baseline recorded"));
    }

    [Test]
    public void BuildToastTitle_TotalsChangesAcrossEveryList()
    {
        var artists = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Artist, "a", "b"),
            Snapshot(Today, TopEntityType.Artist, "b", "a"));
        var tracks = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Track, "x", "y"),
            Snapshot(Today, TopEntityType.Track, "z", "x"));

        // Artists: two swapped. Tracks: z entered, y left, x slipped one.
        Assert.That(MonitorReport.BuildToastTitle(Result(artists, tracks)), Is.EqualTo("Spotify: 1 new, 1 out, 3 moved"));
    }

    [Test]
    public void BuildToastTitle_WhenNothingMoved_SaysNoChange()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Artist, "a", "b"),
            Snapshot(Today, TopEntityType.Artist, "a", "b"));

        Assert.That(MonitorReport.BuildToastTitle(Result(diff)), Is.EqualTo("Spotify: no change in your top lists"));
    }

    [Test]
    public void BuildToastBody_CapsLinesAndCountsTheRemainder()
    {
        var previous = Snapshot(Yesterday, TopEntityType.Artist, "a", "b", "c", "d", "e", "f");
        var current = Snapshot(Today, TopEntityType.Artist, "f", "e", "d", "c", "b", "a");

        var body = MonitorReport.BuildToastBody(Result(TopDiffCalculator.Compare(previous, current)), maxLines: 3);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            // Three highlights plus the "and N more" tail; all six entries moved.
            Assert.That(lines, Has.Length.EqualTo(4));
            Assert.That(lines[3].Trim(), Is.EqualTo("...and 3 more"));
        });
    }

    [Test]
    public void BuildToastBody_RanksEntriesAndExitsAboveSmallShuffles()
    {
        var previous = Snapshot(Yesterday, TopEntityType.Artist, "a", "b", "c");
        var current = Snapshot(Today, TopEntityType.Artist, "a", "c", "z");

        // "z" entered and "b" left; "c" only moved one place, so it should be squeezed out.
        var body = MonitorReport.BuildToastBody(Result(TopDiffCalculator.Compare(previous, current)), maxLines: 2);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var highlights = lines.Take(2).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(highlights, Has.Exactly(1).Contains("entered"));
            Assert.That(highlights, Has.Exactly(1).Contains("no longer"));
            Assert.That(highlights, Has.None.Contains("moved"));
        });
    }

    [Test]
    public void BuildLines_IncludesAHeadingAndEveryChange()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Track, "a", "b"),
            Snapshot(Today, TopEntityType.Track, "b", "a"));

        var lines = MonitorReport.BuildLines(Result(diff));

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Does.Contain("Top tracks (the last 4 weeks)"));
            Assert.That(lines[0], Does.Contain("2 moved"));
            Assert.That(lines, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void NewListAlongsideUnchangedOnes_IsStillWorthNotifying()
    {
        // An existing list that did not move, plus a list tracked for the first time (e.g. after
        // adding "long" to the config). The legacy monitor stayed silent in exactly this case.
        var unchanged = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Artist, "a", "b"),
            Snapshot(Today, TopEntityType.Artist, "a", "b"));
        var newList = TopDiffCalculator.Compare(null, Snapshot(Today, TopEntityType.Track, "x"));
        var result = Result(unchanged, newList);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBaseline, Is.False);
            Assert.That(result.HasNewLists, Is.True);
            Assert.That(result.IsWorthNotifying, Is.True);
            Assert.That(MonitorReport.BuildToastTitle(result), Is.EqualTo("Spotify: started tracking a new list"));
            Assert.That(MonitorReport.BuildToastBody(result), Does.Contain("Top tracks"));
        });
    }

    [Test]
    public void UnchangedRun_IsNotWorthNotifying()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, TopEntityType.Artist, "a"),
            Snapshot(Today, TopEntityType.Artist, "a"));

        Assert.That(Result(diff).IsWorthNotifying, Is.False);
    }

    [Test]
    public void BuildLines_OnFirstRun_ExplainsThereIsNothingToCompare()
    {
        var lines = MonitorReport.BuildLines(
            Result(TopDiffCalculator.Compare(null, Snapshot(Today, TopEntityType.Artist, "a"))));

        Assert.That(lines[1], Does.Contain("baseline"));
    }

    #endregion

}
