using core.playlists;

namespace core.test;

public class ArtistSpreadTests
{

    #region Helper Methods

    /// <summary>One entry per argument; "A+B" is a track by A featuring B.</summary>
    private static List<PlaylistEntry> Entries(params string[] artists)
    {
        return artists.Select((a, i) => new PlaylistEntry($"spotify:track:{i}", $"{i} {a}", a.Split('+', StringSplitOptions.RemoveEmptyEntries))).ToList();
    }

    private static readonly int[] Unchanged4 = [0, 1, 2, 3];

    #endregion

    #region Arrange Tests

    [Test]
    public void Arrange_LeavesAPlaylistWithoutClashesAlone()
    {
        var order = ArtistSpread.Arrange(Entries("A", "B", "A", "C"));

        Assert.That(order, Is.EqualTo(Unchanged4));
    }

    [Test]
    public void Arrange_MovesOnlyTheClashingTrackToTheNearestFreeSlot()
    {
        var order = ArtistSpread.Arrange(Entries("A", "A", "B", "C", "D"));

        Assert.That(order, Is.EqualTo(new[] { 0, 2, 1, 3, 4 }));
    }

    [Test]
    public void Arrange_TreatsAFeaturedArtistAsAClash()
    {
        var items = Entries("A", "B+A", "C", "D");

        var order = ArtistSpread.Arrange(items);

        Assert.Multiple(() =>
        {
            Assert.That(ArtistSpread.CountClashes(items, Unchanged4), Is.EqualTo(1));
            Assert.That(ArtistSpread.CountClashes(items, order), Is.Zero);
            Assert.That(order.OrderBy(i => i), Is.EqualTo(Unchanged4));
        });
    }

    [Test]
    public void Arrange_BreaksUpARunOfOneArtist()
    {
        var items = Entries("A", "A", "A", "B", "C", "D", "E");

        var order = ArtistSpread.Arrange(items);

        Assert.Multiple(() =>
        {
            Assert.That(ArtistSpread.CountClashes(items, order), Is.Zero);
            Assert.That(order.OrderBy(i => i), Is.EqualTo(Enumerable.Range(0, items.Count)));
        });
    }

    [Test]
    public void Arrange_DoesNotSwapOneClashForAnother()
    {
        // Taking the middle track out would only make its neighbours clash; the first one can go.
        var items = Entries("A+B", "A", "B", "C", "D");

        var order = ArtistSpread.Arrange(items);

        Assert.That(ArtistSpread.CountClashes(items, order), Is.Zero);
    }

    [Test]
    public void Arrange_LeavesWhatCannotBePulledApart()
    {
        var items = Entries("A", "A", "A", "B");

        var order = ArtistSpread.Arrange(items);

        Assert.Multiple(() =>
        {
            Assert.That(order.OrderBy(i => i), Is.EqualTo(Unchanged4));
            Assert.That(ArtistSpread.CountClashes(items, order), Is.EqualTo(1));
        });
    }

    [Test]
    public void Arrange_IgnoresTracksWithoutArtists()
    {
        var order = ArtistSpread.Arrange(Entries("", "", "A"));

        Assert.That(order, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    #endregion

}
