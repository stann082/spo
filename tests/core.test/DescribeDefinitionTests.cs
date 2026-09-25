using core.playlists;

namespace core.test;

public class DescribeDefinitionTests
{

    #region Setup

    private string _directory;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-describe-test-{Guid.NewGuid():N}");
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
        var path = Path.Combine(_directory, "describe.json");
        File.WriteAllText(path, json);
        return path;
    }

    #endregion

    #region Tests

    [Test]
    public void Load_ReadsAndTrimsEntries()
    {
        var definition = DescribeDefinition.Load(WriteFile("""
            { "playlists": [ { "name": " Soft Landing ", "description": "  Smooth jazz, nowhere to rush.  " } ] }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(definition.Playlists.Single().Name, Is.EqualTo("Soft Landing"));
            Assert.That(definition.Playlists.Single().Description, Is.EqualTo("Smooth jazz, nowhere to rush."));
        });
    }

    [Test]
    public void Load_ReportsEveryProblemAtOnce()
    {
        var tooLong = new string('x', DescribeDefinition.MaxDescriptionLength + 1);
        var ex = Assert.Throws<SpoException>(() => DescribeDefinition.Load(WriteFile($$"""
            {
              "playlists": [
                { "description": "no name" },
                { "name": "Empty", "description": " " },
                { "name": "Long", "description": "{{tooLong}}" },
                { "name": "Lines", "description": "one\ntwo" },
                { "name": "Twice", "description": "a" },
                { "name": "twice", "description": "b" }
              ]
            }
            """)));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("Playlist #1 needs a \"name\""));
            Assert.That(ex.Message, Does.Contain("'Empty' needs a \"description\""));
            Assert.That(ex.Message, Does.Contain("'Long': the description is 301 characters"));
            Assert.That(ex.Message, Does.Contain("'Lines': the description has a line break"));
            Assert.That(ex.Message, Does.Contain("'Twice' is listed more than once"));
        });
    }

    [Test]
    public void Load_RefusesAnEmptyList()
    {
        var ex = Assert.Throws<SpoException>(() => DescribeDefinition.Load(WriteFile("""{ "playlists": [] }""")));

        Assert.That(ex.Message, Does.Contain("at least one playlist"));
    }

    #endregion

}
