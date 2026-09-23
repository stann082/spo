using core.playlists;

namespace core.test;

public class RemovePlanTests
{

    #region Helper Methods

    private static readonly IReadOnlyList<SourceItem> Items =
    [
        new("spotify:track:a", "A"),
        new("spotify:track:b", "B"),
        new("spotify:track:a", "A again")
    ];

    private static PlaylistDefinition Definition(params TrackDefinition[] tracks)
    {
        return new PlaylistDefinition { Name = "Mix", Tracks = tracks.ToList() };
    }

    #endregion

    #region Build Tests

    [Test]
    public void Build_RemovesTracksInFileOrderWithoutRepeats()
    {
        var plan = RemovePlan.Build(Definition(new() { Id = "b" }, new() { Id = "a" }, new() { Id = "spotify:track:b" }), "Mix", Items);

        Assert.That(plan.Removing.Select(r => r.Display), Is.EqualTo(new[] { "B", "A" }));
    }

    [Test]
    public void Build_RefusesTracksThatAreNotInThePlaylist()
    {
        // Also what makes a rerun safe: once removed, the ids are gone from the playlist.
        var ex = Assert.Throws<SpoException>(() =>
            RemovePlan.Build(Definition(new() { Id = "a" }, new() { Id = "gone", Title = "Gone", Artist = "Someone" }), "Mix", Items));

        Assert.That(ex.Message, Does.Contain("Gone — Someone (gone)").And.Contain("not in 'Mix'"));
    }

    [Test]
    public void Build_RefusesTracksWithoutAnIdAndReportsEveryProblemAtOnce()
    {
        var ex = Assert.Throws<SpoException>(() =>
            RemovePlan.Build(Definition(new() { Title = "Search me", Artist = "Someone" }, new() { Id = "gone" }), "Mix", Items));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("needs an \"id\" (missing on #1)"));
            Assert.That(ex.Message, Does.Contain("gone"));
        });
    }

    [Test]
    public void Build_RefusesAnEmptyTrackList()
    {
        var ex = Assert.Throws<SpoException>(() => RemovePlan.Build(Definition(), "Mix", Items));

        Assert.That(ex.Message, Does.Contain("at least one track"));
    }

    #endregion

}
