using core.plays;

namespace core.test;

public class SqlitePlayStoreTests
{

    #region Setup

    private string _databasePath;
    private SqlitePlayStore _store;

    [SetUp]
    public async Task Setup()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"spo-test-{Guid.NewGuid():N}.db");
        _store = new SqlitePlayStore(_databasePath);
        await _store.InitializeAsync();
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    #endregion

    #region Helper Methods

    private static readonly DateTime Noon = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static PlayRecord Play(int minutesAfterNoon, string trackId, string name = null, string artists = "Someone")
    {
        return new PlayRecord(Noon.AddMinutes(minutesAfterNoon), trackId, name ?? $"Song {trackId}", artists);
    }

    #endregion

    #region Tests

    [Test]
    public void InitializeAsync_IsSafeToCallTwice()
    {
        // Setup already initialised the store; every run of the monitor calls this again.
        Assert.DoesNotThrowAsync(() => _store.InitializeAsync());
    }

    [Test]
    public async Task InitializeAsync_SharesTheFileWithTheSnapshotStore()
    {
        var snapshots = new core.monitor.SqliteSnapshotStore(_databasePath);

        await snapshots.InitializeAsync();
        await _store.SaveAsync([Play(1, "a")]);

        Assert.Multiple(async () =>
        {
            Assert.That(await snapshots.GetLatestAsync(core.monitor.TopEntityType.Track, "short"), Is.Null);
            Assert.That((await _store.GetSummaryAsync()).Plays, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetLatestPlayedAtAsync_WithEmptyLog_ReturnsNull()
    {
        Assert.That(await _store.GetLatestPlayedAtAsync(), Is.Null);
    }

    [Test]
    public async Task GetLatestPlayedAtAsync_ReturnsTheMostRecentPlay()
    {
        await _store.SaveAsync([Play(5, "b"), Play(9, "c"), Play(1, "a")]);

        Assert.That(await _store.GetLatestPlayedAtAsync(), Is.EqualTo(Noon.AddMinutes(9)));
    }

    [Test]
    public async Task SaveAsync_IgnoresPlaysAlreadyOnRecord()
    {
        // Every poll returns the plays of the one before until fifty newer ones push them out.
        int first = await _store.SaveAsync([Play(1, "a"), Play(2, "b")]);
        int second = await _store.SaveAsync([Play(1, "a"), Play(2, "b"), Play(3, "a")]);

        Assert.Multiple(async () =>
        {
            Assert.That(first, Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(1));
            Assert.That((await _store.GetSummaryAsync()).Plays, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetCountsAsync_CountsPlaysPerTrackMostPlayedFirst()
    {
        await _store.SaveAsync([Play(1, "a"), Play(2, "b"), Play(3, "a"), Play(4, "c"), Play(5, "a"), Play(6, "c")]);

        var counts = await _store.GetCountsAsync(10);

        Assert.Multiple(() =>
        {
            Assert.That(counts.Select(c => (c.TrackId, c.Plays)), Is.EqualTo(new[] { ("a", 3), ("c", 2), ("b", 1) }));
            Assert.That(counts[0].Name, Is.EqualTo("Song a"));
            Assert.That(counts[0].FirstPlayed, Is.EqualTo(Noon.AddMinutes(1)));
            Assert.That(counts[0].LastPlayed, Is.EqualTo(Noon.AddMinutes(5)));
        });
    }

    [Test]
    public async Task GetCountsAsync_HonoursTheLimit()
    {
        await _store.SaveAsync([Play(1, "a"), Play(2, "b"), Play(3, "a"), Play(4, "c")]);

        var counts = await _store.GetCountsAsync(1);

        Assert.That(counts.Single().TrackId, Is.EqualTo("a"));
    }

    [Test]
    public async Task GetCountsAsync_FiltersByTitleOrArtistIgnoringCase()
    {
        await _store.SaveAsync(
        [
            Play(1, "a", "Blind", "Korn"),
            Play(2, "b", "Duality", "Slipknot"),
            Play(3, "c", "100% Pure Love", "Crystal Waters")
        ]);

        var byArtist = await _store.GetCountsAsync(10, "korn");
        var byTitle = await _store.GetCountsAsync(10, "DUAL");
        var literalPercent = await _store.GetCountsAsync(10, "100%");

        Assert.Multiple(() =>
        {
            Assert.That(byArtist.Single().TrackId, Is.EqualTo("a"));
            Assert.That(byTitle.Single().TrackId, Is.EqualTo("b"));
            Assert.That(literalPercent.Single().TrackId, Is.EqualTo("c"));
        });
    }

    [Test]
    public async Task GetCountsAsync_CountsOnlyPlaysSinceTheGivenMoment()
    {
        await _store.SaveAsync([Play(1, "a"), Play(2, "a"), Play(10, "a"), Play(11, "b")]);

        var counts = await _store.GetCountsAsync(10, sinceUtc: Noon.AddMinutes(10));

        Assert.That(counts.Select(c => (c.TrackId, c.Plays)), Is.EquivalentTo(new[] { ("a", 1), ("b", 1) }));
    }

    [Test]
    public async Task GetSummaryAsync_DescribesTheWholeLog()
    {
        var empty = await _store.GetSummaryAsync();
        await _store.SaveAsync([Play(1, "a"), Play(2, "b"), Play(3, "a")]);
        var filled = await _store.GetSummaryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.EqualTo(new PlayLogSummary(0, 0, null)));
            Assert.That(filled, Is.EqualTo(new PlayLogSummary(3, 2, Noon.AddMinutes(1))));
        });
    }

    #endregion

}
