using core.playlists;

namespace core.test;

public class ReorderDefinitionTests
{

    #region Setup

    private string _directory;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-reorder-test-{Guid.NewGuid():N}");
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
        var path = Path.Combine(_directory, "reorder.json");
        File.WriteAllText(path, json);
        return path;
    }

    #endregion

    #region Tests

    [Test]
    public void Load_ReadsAnExplicitOrder()
    {
        var definition = ReorderDefinition.Load(WriteFile("""
            { "name": " Wet Ceiling ", "tracks": [ { "id": "b" }, { "id": "a", "title": "A", "artist": "Someone" } ] }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(definition.Name, Is.EqualTo("Wet Ceiling"));
            Assert.That(definition.SpreadArtists, Is.False);
            Assert.That(definition.Tracks.Select(t => t.Id), Is.EqualTo(new[] { "b", "a" }));
        });
    }

    [Test]
    public void Load_ReadsASpreadRequest()
    {
        var definition = ReorderDefinition.Load(WriteFile("""{ "name": "Wet Ceiling", "spreadArtists": true }"""));

        Assert.Multiple(() =>
        {
            Assert.That(definition.SpreadArtists, Is.True);
            Assert.That(definition.Tracks, Is.Empty);
        });
    }

    [Test]
    public void Load_RefusesAFileThatSaysNeitherOrBoth()
    {
        var neither = Assert.Throws<SpoException>(() => ReorderDefinition.Load(WriteFile("""{ "name": "Mix" }""")));
        var both = Assert.Throws<SpoException>(() => ReorderDefinition.Load(WriteFile("""
            { "name": "Mix", "spreadArtists": true, "tracks": [ { "id": "a" } ] }
            """)));

        Assert.Multiple(() =>
        {
            Assert.That(neither.Message, Does.Contain("Give \"tracks\" (the whole new order) or \"spreadArtists\": true."));
            Assert.That(both.Message, Does.Contain("not both"));
        });
    }

    [Test]
    public void Load_ReportsEveryProblemAtOnce()
    {
        var ex = Assert.Throws<SpoException>(() => ReorderDefinition.Load(WriteFile("""
            { "tracks": [ { "id": "a" }, { "title": "Search me", "artist": "Someone" } ] }
            """)));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("\"name\" must name the playlist to reorder."));
            Assert.That(ex.Message, Does.Contain("needs an \"id\" (missing on #2)"));
        });
    }

    #endregion

}
