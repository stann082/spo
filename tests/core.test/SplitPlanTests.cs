using core.playlists;

namespace core.test;

public class SplitPlanTests
{

    #region Setup

    private string _directory;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-split-test-{Guid.NewGuid():N}");
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
        new("spotify:track:c", "C"),
        new("spotify:local:x", "Local file")
    ];

    private static SplitDefinition Definition(params (string name, string[] ids)[] playlists)
    {
        return new SplitDefinition
        {
            Source = "Mix",
            Playlists = playlists
                .Select(p => new PlaylistDefinition
                {
                    Name = p.name,
                    Tracks = p.ids.Select(id => new TrackDefinition { Id = id }).ToList()
                })
                .ToList()
        };
    }

    private string WriteFile(string json)
    {
        var path = Path.Combine(_directory, "split.json");
        File.WriteAllText(path, json);
        return path;
    }

    #endregion

    #region Build Tests

    [Test]
    public void Build_AssignsTracksAndLeavesTheRestAsOrphansInSourceOrder()
    {
        var plan = SplitPlan.Build(Definition(("One", ["c", "a"])), "Mix", Source, ["Mix"]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Targets[0].Uris, Is.EqualTo(new[] { "spotify:track:c", "spotify:track:a" }));
            Assert.That(plan.Orphans.Select(o => o.Display), Is.EqualTo(new[] { "B", "Local file" }));
        });
    }

    [Test]
    public void Build_RefusesTracksThatAreNotInTheSource()
    {
        // Also what makes a rerun safe: once moved, the ids are gone from the source.
        var ex = Assert.Throws<SpoException>(() =>
            SplitPlan.Build(Definition(("One", ["a", "gone"])), "Mix", Source, ["Mix"]));

        Assert.That(ex.Message, Does.Contain("gone").And.Contain("not in 'Mix'"));
    }

    [Test]
    public void Build_RefusesANameThatAlreadyExists()
    {
        var ex = Assert.Throws<SpoException>(() =>
            SplitPlan.Build(Definition(("old-school ebm", ["a"])), "Mix", Source, ["Mix", "Old-School EBM"]));

        Assert.That(ex.Message, Does.Contain("already exists"));
    }

    [Test]
    public void Build_ReportsEveryProblemAtOnce()
    {
        var ex = Assert.Throws<SpoException>(() =>
            SplitPlan.Build(Definition(("Taken", ["a"]), ("Two", ["nope"])), "Mix", Source, ["Taken"]));

        Assert.That(ex.Message, Does.Contain("already exists").And.Contain("nope"));
    }

    [Test]
    public void Build_DropsRepeatsWithinATargetAndAllowsATrackInTwoTargets()
    {
        var plan = SplitPlan.Build(Definition(("One", ["a", "a"]), ("Two", ["a", "b"])), "Mix", Source, []);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Targets[0].Uris, Is.EqualTo(new[] { "spotify:track:a" }));
            Assert.That(plan.Targets[1].Uris, Is.EqualTo(new[] { "spotify:track:a", "spotify:track:b" }));
            Assert.That(plan.Orphans.Select(o => o.Display), Is.EqualTo(new[] { "C", "Local file" }));
        });
    }

    [Test]
    public void OrphanedName_AppendsTheSuffix()
    {
        Assert.That(SplitPlan.OrphanedName("Cybernetic Pulse"), Is.EqualTo("Cybernetic Pulse_orphaned tracks"));
    }

    #endregion

    #region Load Tests

    [Test]
    public void Load_ReadsSourceAndPlaylists()
    {
        var definition = SplitDefinition.Load(WriteFile("""
            { "source": "Mix", "playlists": [ { "name": "One", "tracks": [ { "id": "a", "title": "A" } ] } ] }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(definition.Source, Is.EqualTo("Mix"));
            Assert.That(definition.Playlists.Single().Tracks.Single().Uri, Is.EqualTo("spotify:track:a"));
        });
    }

    [Test]
    public void Load_ReportsEveryFileProblemAtOnce()
    {
        var path = WriteFile("""
            {
              "playlists": [
                { "name": "One", "tracks": [ { "title": "no id" } ] },
                { "name": "one", "tracks": [ { "id": "a" } ] },
                { "name": "Empty", "tracks": [] }
              ]
            }
            """);

        var ex = Assert.Throws<SpoException>(() => SplitDefinition.Load(path));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("\"source\""));
            Assert.That(ex.Message, Does.Contain("needs an \"id\""));
            Assert.That(ex.Message, Does.Contain("listed more than once"));
            Assert.That(ex.Message, Does.Contain("'Empty' has no tracks"));
        });
    }

    #endregion

}
