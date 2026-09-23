using core.playlists;

namespace core.test;

public class MovePlanTests
{

    #region Setup

    private string _directory;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-move-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, recursive: true);
    }

    #endregion

    #region Helper Methods

    private static readonly IReadOnlyList<SourceItem> Source =
    [
        new("spotify:track:a", "A"),
        new("spotify:track:b", "B"),
        new("spotify:track:c", "C")
    ];

    private static MoveDefinition Definition(params string[] ids)
    {
        return new MoveDefinition
        {
            Source = "Mix",
            Target = "Other",
            Tracks = ids.Select(id => new TrackDefinition { Id = id }).ToList()
        };
    }

    private string WriteFile(string json)
    {
        var path = Path.Combine(_directory, "move.json");
        File.WriteAllText(path, json);
        return path;
    }

    #endregion

    #region Build Tests

    [Test]
    public void Build_MovesTracksInFileOrderWithoutRepeats()
    {
        var plan = MovePlan.Build(Definition("c", "a", "c"), "Mix", Source, []);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Moving.Select(m => m.Display), Is.EqualTo(new[] { "C", "A" }));
            Assert.That(plan.ToAdd.Select(m => m.Display), Is.EqualTo(new[] { "C", "A" }));
            Assert.That(plan.AlreadyInTarget, Is.Empty);
        });
    }

    [Test]
    public void Build_OnlyRemovesTracksTheTargetAlreadyHas()
    {
        var plan = MovePlan.Build(Definition("a", "b"), "Mix", Source, ["spotify:track:b", "spotify:track:z"]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Moving.Select(m => m.Display), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(plan.ToAdd.Select(m => m.Display), Is.EqualTo(new[] { "A" }));
            Assert.That(plan.AlreadyInTarget.Select(m => m.Display), Is.EqualTo(new[] { "B" }));
        });
    }

    [Test]
    public void Build_RefusesTracksThatAreNotInTheSource()
    {
        // Also what makes a rerun safe: once moved, the ids are gone from the source.
        var ex = Assert.Throws<SpoException>(() => MovePlan.Build(Definition("a", "gone"), "Mix", Source, []));

        Assert.That(ex.Message, Does.Contain("gone").And.Contain("not in 'Mix'"));
    }

    #endregion

    #region Load Tests

    [Test]
    public void Load_ReadsSourceTargetAndTracks()
    {
        var definition = MoveDefinition.Load(WriteFile("""
            { "source": "Mix", "target": "Other", "tracks": [ { "id": "a", "title": "A" } ] }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(definition.Source, Is.EqualTo("Mix"));
            Assert.That(definition.Target, Is.EqualTo("Other"));
            Assert.That(definition.Tracks.Single().Uri, Is.EqualTo("spotify:track:a"));
        });
    }

    [Test]
    public void Load_ReportsEveryFileProblemAtOnce()
    {
        var ex = Assert.Throws<SpoException>(() => MoveDefinition.Load(WriteFile("""
            { "tracks": [ { "title": "no id" } ] }
            """)));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("\"source\""));
            Assert.That(ex.Message, Does.Contain("\"target\""));
            Assert.That(ex.Message, Does.Contain("needs an \"id\""));
        });
    }

    [Test]
    public void Load_RefusesTheSamePlaylistAsSourceAndTarget()
    {
        var ex = Assert.Throws<SpoException>(() => MoveDefinition.Load(WriteFile("""
            { "source": "Mix", "target": " mix ", "tracks": [ { "id": "a" } ] }
            """)));

        Assert.That(ex.Message, Does.Contain("same playlist"));
    }

    [Test]
    public void Load_RefusesAnEmptyTrackList()
    {
        var ex = Assert.Throws<SpoException>(() => MoveDefinition.Load(WriteFile("""
            { "source": "Mix", "target": "Other", "tracks": [] }
            """)));

        Assert.That(ex.Message, Does.Contain("at least one track"));
    }

    #endregion

}
