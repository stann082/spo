using core.playlists;

namespace core.test;

public class PlaylistImportResultTests
{

    #region Tests

    [Test]
    public void Assemble_KeepsTheFileOrderAcrossIdsAndSearches()
    {
        // Legacy added every id first and every search hit after, reordering the playlist.
        var searchFirst = new TrackDefinition { Title = "First" };
        var searchThird = new TrackDefinition { Title = "Third" };
        var definition = new PlaylistDefinition
        {
            Name = "X",
            Tracks = [searchFirst, new TrackDefinition { Id = "second" }, searchThird, new TrackDefinition { Id = "fourth" }]
        };
        var hits = new Dictionary<TrackDefinition, string>
        {
            [searchFirst] = "spotify:track:first",
            [searchThird] = "spotify:track:third"
        };

        var result = PlaylistImportResult.Assemble(definition, hits);

        Assert.That(result.Uris, Is.EqualTo(new[]
        {
            "spotify:track:first", "spotify:track:second", "spotify:track:third", "spotify:track:fourth"
        }));
    }

    [Test]
    public void Assemble_ReportsMissedSearchesAndLeavesThemOut()
    {
        var missing = new TrackDefinition { Title = "Nowhere", Artist = "Nobody" };
        var definition = new PlaylistDefinition
        {
            Name = "X",
            Tracks = [new TrackDefinition { Id = "a" }, missing]
        };
        var hits = new Dictionary<TrackDefinition, string> { [missing] = null };

        var result = PlaylistImportResult.Assemble(definition, hits);

        Assert.Multiple(() =>
        {
            Assert.That(result.Uris, Is.EqualTo(new[] { "spotify:track:a" }));
            Assert.That(result.Requested, Is.EqualTo(2));
            Assert.That(result.NotFound, Is.EqualTo(new[] { missing }));
            Assert.That(missing.ToString(), Is.EqualTo("Nowhere — Nobody"));
        });
    }

    [Test]
    public void Assemble_GroupsSkippedTracksByStatusIgnoringCase()
    {
        var definition = new PlaylistDefinition
        {
            Name = "X",
            Tracks =
            [
                new TrackDefinition { Title = "A", Status = "blocked" },
                new TrackDefinition { Title = "B", Status = "Blocked " },
                new TrackDefinition { Title = "C", Status = "unavailable" }
            ]
        };

        var result = PlaylistImportResult.Assemble(definition, new Dictionary<TrackDefinition, string>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Uris, Is.Empty);
            Assert.That(result.Requested, Is.EqualTo(0));
            Assert.That(result.Skipped.Select(g => g.Count()), Is.EqualTo(new[] { 2, 1 }));
        });
    }

    [Test]
    public void ExcludeExisting_SkipsWhatThePlaylistHasAndRepeatsInTheFile()
    {
        var newOne = new TrackDefinition { Id = "new" };
        var there = new TrackDefinition { Id = "there" };
        var repeat = new TrackDefinition { Id = "spotify:track:new" };
        var definition = new PlaylistDefinition { Name = "X", Tracks = [newOne, there, repeat] };

        var selection = PlaylistImportResult
            .Assemble(definition, new Dictionary<TrackDefinition, string>())
            .ExcludeExisting(["spotify:track:there", "spotify:track:other"]);

        Assert.Multiple(() =>
        {
            Assert.That(selection.ToAdd.Select(t => t.Track), Is.EqualTo(new[] { newOne }));
            Assert.That(selection.AlreadyThere.Select(t => t.Track), Is.EqualTo(new[] { there, repeat }));
        });
    }

    [Test]
    public void ExcludeExisting_KeepsFileOrderForWhatItAdds()
    {
        var hit = new TrackDefinition { Title = "Searched" };
        var definition = new PlaylistDefinition { Name = "X", Tracks = [new TrackDefinition { Id = "b" }, hit, new TrackDefinition { Id = "a" }] };

        var selection = PlaylistImportResult
            .Assemble(definition, new Dictionary<TrackDefinition, string> { [hit] = "spotify:track:s" })
            .ExcludeExisting([]);

        Assert.That(selection.ToAdd.Select(t => t.Uri), Is.EqualTo(new[] { "spotify:track:b", "spotify:track:s", "spotify:track:a" }));
    }

    #endregion

}
