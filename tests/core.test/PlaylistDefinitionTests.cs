using core.playlists;

namespace core.test;

public class PlaylistDefinitionTests
{

    #region Setup

    private string _directory;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-playlist-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, recursive: true);
    }

    #endregion

    #region Helper Methods

    private string WriteFile(string json)
    {
        var path = Path.Combine(_directory, "playlist.json");
        File.WriteAllText(path, json);
        return path;
    }

    #endregion

    #region Tests

    [Test]
    public void Load_ReadsNameDescriptionAndTracks()
    {
        var path = WriteFile("""
            {
              "name": "Road Trip",
              "description": "Loud",
              "tracks": [
                { "id": "abc" },
                { "title": "Paranoid", "artist": "Black Sabbath" },
                { "title": "Gone", "artist": "Someone", "status": "blocked" }
              ]
            }
            """);

        var definition = PlaylistDefinition.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(definition.Name, Is.EqualTo("Road Trip"));
            Assert.That(definition.Description, Is.EqualTo("Loud"));
            Assert.That(definition.Tracks.Select(t => t.Source),
                Is.EqualTo(new[] { TrackSource.Id, TrackSource.Search, TrackSource.Skipped }));
        });
    }

    [Test]
    public void Load_WithoutTracks_GivesAnEmptyList()
    {
        var definition = PlaylistDefinition.Load(WriteFile("""{ "name": "Empty" }"""));

        Assert.That(definition.Tracks, Is.Empty);
    }

    [Test]
    public void Load_MissingFile_Throws()
    {
        var ex = Assert.Throws<SpoException>(() => PlaylistDefinition.Load(Path.Combine(_directory, "nope.json")));

        Assert.That(ex.Message, Does.StartWith("File not found"));
    }

    [Test]
    public void Load_InvalidJson_Throws()
    {
        var ex = Assert.Throws<SpoException>(() => PlaylistDefinition.Load(WriteFile("{ not json")));

        Assert.That(ex.Message, Does.StartWith("Could not parse"));
    }

    [Test]
    public void Load_WithoutAName_Throws()
    {
        var ex = Assert.Throws<SpoException>(() => PlaylistDefinition.Load(WriteFile("""{ "tracks": [] }""")));

        Assert.That(ex.Message, Does.Contain("\"name\""));
    }

    [Test]
    public void Load_TrackWithNeitherIdNorTitle_ThrowsNamingIt()
    {
        // Legacy searched for "track:" here and added whatever Spotify returned first.
        var path = WriteFile("""
            { "name": "X", "tracks": [ { "id": "a" }, { "artist": "Only An Artist" } ] }
            """);

        var ex = Assert.Throws<SpoException>(() => PlaylistDefinition.Load(path));

        Assert.That(ex.Message, Does.Contain("#2"));
    }

    [Test]
    public void Source_IdWinsOverStatus()
    {
        var track = new TrackDefinition { Id = "abc", Status = "blocked" };

        Assert.That(track.Source, Is.EqualTo(TrackSource.Id));
    }

    [TestCase("abc", "spotify:track:abc")]
    [TestCase("spotify:track:abc", "spotify:track:abc")]
    [TestCase("  abc ", "spotify:track:abc")]
    public void Uri_AcceptsBareIdsAndFullUris(string id, string expected)
    {
        Assert.That(new TrackDefinition { Id = id }.Uri, Is.EqualTo(expected));
    }

    [Test]
    public void SearchQuery_IncludesTheArtistOnlyWhenGiven()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new TrackDefinition { Title = "Paranoid", Artist = "Black Sabbath" }.SearchQuery,
                Is.EqualTo("track:Paranoid artist:Black Sabbath"));
            Assert.That(new TrackDefinition { Title = "Paranoid" }.SearchQuery, Is.EqualTo("track:Paranoid"));
        });
    }

    #endregion

}
