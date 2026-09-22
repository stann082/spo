using core.monitor;

namespace core.test;

public class TopDiffCalculatorTests
{

    #region Helper Methods

    private static TopSnapshot Snapshot(DateTime capturedAt, params string[] spotifyIds)
    {
        return new TopSnapshot
        {
            CapturedAt = capturedAt,
            EntityType = TopEntityType.Artist,
            TimeRange = "short",
            Entries = spotifyIds
                .Select((id, index) => new TopEntry(index + 1, id, $"Artist {id}", string.Empty))
                .ToList()
        };
    }

    private static EntryChange ChangeFor(TopDiff diff, string spotifyId)
    {
        return diff.Changes.Single(c => c.SpotifyId == spotifyId);
    }

    private static readonly DateTime Yesterday = new(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

    #endregion

    #region Tests

    [Test]
    public void Compare_WithNoPreviousSnapshot_IsBaselineWithNoChanges()
    {
        var diff = TopDiffCalculator.Compare(null, Snapshot(Today, "a", "b", "c"));

        Assert.Multiple(() =>
        {
            Assert.That(diff.IsBaseline, Is.True);
            Assert.That(diff.HasChanges, Is.False);
            Assert.That(diff.ListSize, Is.EqualTo(3));
            Assert.That(diff.PreviousCapturedAt, Is.Null);
        });
    }

    [Test]
    public void Compare_WithIdenticalSnapshots_ReportsNoChanges()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c"),
            Snapshot(Today, "a", "b", "c"));

        Assert.Multiple(() =>
        {
            Assert.That(diff.IsBaseline, Is.False);
            Assert.That(diff.HasChanges, Is.False);
        });
    }

    [Test]
    public void Compare_WhenEntryClimbs_ReportsMovedUpWithPositiveDelta()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c"),
            Snapshot(Today, "c", "a", "b"));

        var change = ChangeFor(diff, "c");

        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(ChangeKind.MovedUp));
            Assert.That(change.PreviousRank, Is.EqualTo(3));
            Assert.That(change.CurrentRank, Is.EqualTo(1));
            Assert.That(change.Delta, Is.EqualTo(2));
        });
    }

    [Test]
    public void Compare_WhenEntrySlips_ReportsMovedDownWithNegativeDelta()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c"),
            Snapshot(Today, "b", "c", "a"));

        var change = ChangeFor(diff, "a");

        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(ChangeKind.MovedDown));
            Assert.That(change.PreviousRank, Is.EqualTo(1));
            Assert.That(change.CurrentRank, Is.EqualTo(3));
            Assert.That(change.Delta, Is.EqualTo(-2));
        });
    }

    [Test]
    public void Compare_WhenEntryIsNew_ReportsEnteredWithNoPreviousRank()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b"),
            Snapshot(Today, "a", "z", "b"));

        var change = ChangeFor(diff, "z");

        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(ChangeKind.Entered));
            Assert.That(change.PreviousRank, Is.Null);
            Assert.That(change.CurrentRank, Is.EqualTo(2));
            Assert.That(change.Delta, Is.EqualTo(0));
        });
    }

    [Test]
    public void Compare_WhenEntryDropsOut_ReportsLeftWithNoCurrentRank()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c"),
            Snapshot(Today, "a", "b"));

        var change = ChangeFor(diff, "c");

        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(ChangeKind.Left));
            Assert.That(change.PreviousRank, Is.EqualTo(3));
            Assert.That(change.CurrentRank, Is.Null);
            Assert.That(change.Describe(2), Does.Contain("no longer in the top 2"));
        });
    }

    [Test]
    public void Compare_MatchesOnSpotifyIdNotName()
    {
        var previous = new TopSnapshot
        {
            CapturedAt = Yesterday,
            EntityType = TopEntityType.Artist,
            TimeRange = "short",
            Entries = [new TopEntry(1, "id-1", "Old Name", string.Empty)]
        };
        var current = new TopSnapshot
        {
            CapturedAt = Today,
            EntityType = TopEntityType.Artist,
            TimeRange = "short",
            Entries = [new TopEntry(1, "id-1", "New Name", string.Empty)]
        };

        var diff = TopDiffCalculator.Compare(previous, current);

        // A rename at the same rank is not a change worth reporting.
        Assert.That(diff.HasChanges, Is.False);
    }

    [Test]
    public void Compare_OrdersChangesAsEnteredThenLeftThenBiggestMove()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c", "d", "e"),
            Snapshot(Today, "z", "e", "a", "b", "c"));

        // "z" entered, "d" left, then moves ordered by magnitude: e +3, a -2, b -2, c -2.
        Assert.Multiple(() =>
        {
            Assert.That(diff.Changes[0].Kind, Is.EqualTo(ChangeKind.Entered));
            Assert.That(diff.Changes[0].SpotifyId, Is.EqualTo("z"));
            Assert.That(diff.Changes[1].Kind, Is.EqualTo(ChangeKind.Left));
            Assert.That(diff.Changes[1].SpotifyId, Is.EqualTo("d"));
            Assert.That(diff.Changes[2].SpotifyId, Is.EqualTo("e"));
            Assert.That(Math.Abs(diff.Changes[2].Delta), Is.EqualTo(3));
        });
    }

    [Test]
    public void Summarize_CountsEachKindOfChange()
    {
        var diff = TopDiffCalculator.Compare(
            Snapshot(Yesterday, "a", "b", "c"),
            Snapshot(Today, "z", "a", "b"));

        // "z" entered, "c" left, "a" and "b" each slipped one place.
        Assert.That(diff.Summarize(), Is.EqualTo("Top artists: 1 new, 1 out, 2 moved"));
    }

    [Test]
    public void Compare_WithDuplicateIdsInAList_DoesNotThrow()
    {
        var current = new TopSnapshot
        {
            CapturedAt = Today,
            EntityType = TopEntityType.Artist,
            TimeRange = "short",
            Entries =
            [
                new TopEntry(1, "dup", "Artist", string.Empty),
                new TopEntry(2, "dup", "Artist", string.Empty)
            ]
        };

        Assert.DoesNotThrow(() => TopDiffCalculator.Compare(Snapshot(Yesterday, "dup"), current));
    }

    #endregion

}
