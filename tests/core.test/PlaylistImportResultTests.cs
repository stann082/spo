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

    #endregion

}
