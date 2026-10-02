using core.plays;

namespace core.test;

public class PlayLogTests
{

    #region Helper Methods

    private static readonly DateTime Noon = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static PlayRecord Play(int minutesAfterNoon)
    {
        return new PlayRecord(Noon.AddMinutes(minutesAfterNoon), $"track-{minutesAfterNoon}", $"Song {minutesAfterNoon}", "Someone");
    }

    private static List<PlayRecord> Plays(int from, int count)
    {
        return Enumerable.Range(from, count).Select(Play).ToList();
    }

    #endregion

    #region SelectNew Tests

    [Test]
    public void SelectNew_TakesEverythingWhenTheLogIsEmpty()
    {
        var fresh = PlayLog.SelectNew(null, [Play(3), Play(1), Play(2)]);

        // Oldest first, the order they were played in.
        Assert.That(fresh.Select(p => p.TrackId), Is.EqualTo(new[] { "track-1", "track-2", "track-3" }));
    }

    [Test]
    public void SelectNew_SkipsPlaysAlreadyOnRecord()
    {
        var fresh = PlayLog.SelectNew(Noon.AddMinutes(2), [Play(4), Play(3), Play(2), Play(1)]);

        Assert.That(fresh.Select(p => p.TrackId), Is.EqualTo(new[] { "track-3", "track-4" }));
    }

    [Test]
    public void SelectNew_FindsNothingWhenNothingWasPlayedSince()
    {
        var fresh = PlayLog.SelectNew(Noon.AddMinutes(4), [Play(4), Play(3)]);

        Assert.That(fresh, Is.Empty);
    }

    #endregion

    #region MayHaveMissedPlays Tests

    [Test]
    public void MayHaveMissedPlays_WhenAFullPageIsAllNew()
    {
        Assert.That(PlayLog.MayHaveMissedPlays(Noon, Plays(1, 50), 50), Is.True);
    }

    [Test]
    public void MayHaveMissedPlays_NotWhenThePageOverlapsTheLog()
    {
        // The oldest fetched play is the last recorded one, so nothing fell in between.
        Assert.That(PlayLog.MayHaveMissedPlays(Noon, Plays(0, 50), 50), Is.False);
    }

    [Test]
    public void MayHaveMissedPlays_NotWhenThePageIsNotFull()
    {
        Assert.That(PlayLog.MayHaveMissedPlays(Noon, Plays(1, 49), 50), Is.False);
    }

    [Test]
    public void MayHaveMissedPlays_NotOnTheFirstRun()
    {
        Assert.That(PlayLog.MayHaveMissedPlays(null, Plays(1, 50), 50), Is.False);
    }

    #endregion

}
