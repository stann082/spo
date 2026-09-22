using core.monitor;

namespace core.test;

public class SqliteSnapshotStoreTests
{

    #region Setup

    private string _databasePath;
    private SqliteSnapshotStore _store;

    [SetUp]
    public async Task Setup()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"spo-test-{Guid.NewGuid():N}.db");
        _store = new SqliteSnapshotStore(_databasePath);
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

    private static TopSnapshot Snapshot(DateTime capturedAt, TopEntityType entityType, string timeRange, params string[] names)
    {
        return new TopSnapshot
        {
            CapturedAt = capturedAt,
            EntityType = entityType,
            TimeRange = timeRange,
            Entries = names
                .Select((name, index) => new TopEntry(index + 1, $"id-{name}", name, $"detail-{name}"))
                .ToList()
        };
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
    public async Task GetLatestAsync_WithEmptyStore_ReturnsNull()
    {
        var latest = await _store.GetLatestAsync(TopEntityType.Artist, "short");

        Assert.That(latest, Is.Null);
    }

    [Test]
    public async Task SaveAsync_ThenGetLatest_RoundTripsEveryField()
    {
        var capturedAt = new DateTime(2026, 7, 28, 4, 30, 15, DateTimeKind.Utc);
        await _store.SaveAsync(Snapshot(capturedAt, TopEntityType.Track, "medium", "Alpha", "Beta"));

        var latest = await _store.GetLatestAsync(TopEntityType.Track, "medium");

        Assert.That(latest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(latest.CapturedAt, Is.EqualTo(capturedAt));
            Assert.That(latest.EntityType, Is.EqualTo(TopEntityType.Track));
            Assert.That(latest.TimeRange, Is.EqualTo("medium"));
            Assert.That(latest.Entries, Has.Count.EqualTo(2));
            Assert.That(latest.Entries[0].Rank, Is.EqualTo(1));
            Assert.That(latest.Entries[0].SpotifyId, Is.EqualTo("id-Alpha"));
            Assert.That(latest.Entries[0].Name, Is.EqualTo("Alpha"));
            Assert.That(latest.Entries[0].Detail, Is.EqualTo("detail-Alpha"));
        });
    }

    [Test]
    public async Task SaveAsync_AssignsTheSnapshotId()
    {
        var snapshot = Snapshot(DateTime.UtcNow, TopEntityType.Artist, "short", "Alpha");

        await _store.SaveAsync(snapshot);

        Assert.That(snapshot.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetLatestAsync_ReturnsTheMostRecentSnapshot()
    {
        var older = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

        await _store.SaveAsync(Snapshot(older, TopEntityType.Artist, "short", "Old"));
        await _store.SaveAsync(Snapshot(newer, TopEntityType.Artist, "short", "New"));

        var latest = await _store.GetLatestAsync(TopEntityType.Artist, "short");

        Assert.Multiple(() =>
        {
            Assert.That(latest.CapturedAt, Is.EqualTo(newer));
            Assert.That(latest.Entries[0].Name, Is.EqualTo("New"));
        });
    }

    [Test]
    public async Task GetLatestAsync_KeepsListsWithDifferentTypesAndRangesApart()
    {
        var capturedAt = DateTime.UtcNow;
        await _store.SaveAsync(Snapshot(capturedAt, TopEntityType.Artist, "short", "ShortArtist"));
        await _store.SaveAsync(Snapshot(capturedAt, TopEntityType.Artist, "long", "LongArtist"));
        await _store.SaveAsync(Snapshot(capturedAt, TopEntityType.Track, "short", "ShortTrack"));

        var shortArtists = await _store.GetLatestAsync(TopEntityType.Artist, "short");
        var longArtists = await _store.GetLatestAsync(TopEntityType.Artist, "long");
        var shortTracks = await _store.GetLatestAsync(TopEntityType.Track, "short");
        var mediumTracks = await _store.GetLatestAsync(TopEntityType.Track, "medium");

        Assert.Multiple(() =>
        {
            Assert.That(shortArtists.Entries[0].Name, Is.EqualTo("ShortArtist"));
            Assert.That(longArtists.Entries[0].Name, Is.EqualTo("LongArtist"));
            Assert.That(shortTracks.Entries[0].Name, Is.EqualTo("ShortTrack"));
            Assert.That(mediumTracks, Is.Null);
        });
    }

    [Test]
    public async Task PruneAsync_RemovesOnlySnapshotsBeforeTheCutoff()
    {
        var old = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);

        await _store.SaveAsync(Snapshot(old, TopEntityType.Artist, "short", "Old"));
        await _store.SaveAsync(Snapshot(recent, TopEntityType.Artist, "short", "Recent"));

        int pruned = await _store.PruneAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var latest = await _store.GetLatestAsync(TopEntityType.Artist, "short");

        Assert.Multiple(() =>
        {
            Assert.That(pruned, Is.EqualTo(1));
            Assert.That(latest.Entries[0].Name, Is.EqualTo("Recent"));
        });
    }

    [Test]
    public async Task PruneAsync_CascadesToTheEntriesOfDeletedSnapshots()
    {
        var old = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _store.SaveAsync(Snapshot(old, TopEntityType.Artist, "short", "Old"));

        await _store.PruneAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(await _store.GetLatestAsync(TopEntityType.Artist, "short"), Is.Null);
    }

    [Test]
    public async Task SaveAsync_StoresAFullFiftyEntryList()
    {
        var names = Enumerable.Range(1, 50).Select(i => $"Entry{i}").ToArray();
        await _store.SaveAsync(Snapshot(DateTime.UtcNow, TopEntityType.Track, "short", names));

        var latest = await _store.GetLatestAsync(TopEntityType.Track, "short");

        Assert.Multiple(() =>
        {
            Assert.That(latest.Entries, Has.Count.EqualTo(50));
            Assert.That(latest.Entries[49].Rank, Is.EqualTo(50));
            Assert.That(latest.Entries.Select(e => e.Rank), Is.Ordered);
        });
    }

    #endregion

}
