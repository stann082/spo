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

    private static TrackMatch Track(string id, string name = null, string artists = "Someone")
    {
        return new TrackMatch(id, name ?? $"Song {id}", artists, artists.Split(", ")[0]);
    }

    [Test]
    public async Task GetCountsAsync_CountsOnlyTheGivenTracks()
    {
        await _store.SaveAsync([Play(1, "a"), Play(2, "b"), Play(3, "a"), Play(4, "c")]);

        var counts = await _store.GetCountsAsync(10, tracks: [Track("a"), Track("c"), Track("never-played")]);
        var none = await _store.GetCountsAsync(10, tracks: []);

        Assert.Multiple(() =>
        {
            Assert.That(counts.Select(c => (c.TrackId, c.Plays)), Is.EqualTo(new[] { ("a", 2), ("c", 1) }));
            Assert.That(none, Is.Empty);
        });
    }

    [Test]
    public async Task GetCountsAsync_TakesMoreTracksThanSqliteTakesParameters()
    {
        // A big playlist easily passes the 999 parameters older SQLite builds allow.
        await _store.SaveAsync([Play(1, "id-1500"), Play(2, "elsewhere")]);
        var tracks = Enumerable.Range(0, 2000).Select(i => Track($"id-{i}")).ToList();

        var counts = await _store.GetCountsAsync(10, tracks: tracks);

        Assert.That(counts.Single().TrackId, Is.EqualTo("id-1500"));
    }

    [Test]
    public async Task GetCountsAsync_CombinesTheTrackFilterWithTheOthers()
    {
        await _store.SaveAsync([Play(1, "a", "Walk", "Pantera"), Play(20, "a", "Walk", "Pantera"), Play(21, "b", "Blind", "Korn")]);

        var counts = await _store.GetCountsAsync(10, query: "pantera", sinceUtc: Noon.AddMinutes(10), tracks: [Track("a", "Walk", "Pantera"), Track("b", "Blind", "Korn")]);

        Assert.That(counts.Select(c => (c.TrackId, c.Plays)), Is.EqualTo(new[] { ("a", 1) }));
    }

    [Test]
    public async Task GetCountsAsync_CountsAnotherReleaseOfTheSameSongUnderTheGivenTrack()
    {
        // The playlist holds the album track; the single was played from the artist's page.
        await _store.SaveAsync(
        [
            Play(1, "album", "New Rules", "Dua Lipa"),
            Play(2, "single", "new rules", "DUA LIPA"),
            Play(3, "single", "New Rules", "Dua Lipa"),
            Play(4, "feature", "New Rules", "Dua Lipa, Someone Else")
        ]);

        var counts = await _store.GetCountsAsync(10, tracks: [new TrackMatch("album", "New Rules", "Dua Lipa", "Dua Lipa")]);

        Assert.Multiple(() =>
        {
            Assert.That(counts.Single().TrackId, Is.EqualTo("album"));
            Assert.That(counts.Single().Plays, Is.EqualTo(4));
            Assert.That(counts.Single().FirstPlayed, Is.EqualTo(Noon.AddMinutes(1)));
            Assert.That(counts.Single().LastPlayed, Is.EqualTo(Noon.AddMinutes(4)));
        });
    }

    [Test]
    public async Task GetCountsAsync_DoesNotMatchOtherVersionsOrOtherArtists()
    {
        await _store.SaveAsync(
        [
            Play(1, "remaster", "Walk - Remastered", "Pantera"),
            Play(2, "cover", "Walk", "Someone Else, Pantera"),
            Play(3, "prefix", "Walk", "Pantera Tribute Band"),
            Play(4, "other", "Walk", "The Pantera")
        ]);

        var counts = await _store.GetCountsAsync(10, tracks: [Track("album", "Walk", "Pantera")]);

        Assert.That(counts, Is.Empty);
    }

    [Test]
    public async Task GetCountsAsync_MatchesALeadArtistWhoseNameHasAComma()
    {
        await _store.SaveAsync([Play(1, "single", "EARFQUAKE", "Tyler, The Creator"), Play(2, "remix", "EARFQUAKE", "Tyler, The Creator, Playboi Carti")]);

        var counts = await _store.GetCountsAsync(10, tracks: [new TrackMatch("album", "EARFQUAKE", "Tyler, The Creator", "Tyler, The Creator")]);

        Assert.That(counts.Single().Plays, Is.EqualTo(2));
    }

    [Test]
    public async Task GetCountsAsync_CountsAPlayOnceWhenThePlaylistHasTwoReleasesOfTheSong()
    {
        // Each play goes to the track with its own id, not to both.
        await _store.SaveAsync([Play(1, "album", "Blinding Lights", "The Weeknd"), Play(2, "single", "Blinding Lights", "The Weeknd"), Play(3, "single", "Blinding Lights", "The Weeknd")]);

        var counts = await _store.GetCountsAsync(10, tracks:
        [
            new TrackMatch("album", "Blinding Lights", "The Weeknd", "The Weeknd"),
            new TrackMatch("single", "Blinding Lights", "The Weeknd", "The Weeknd")
        ]);

        Assert.That(counts.Select(c => (c.TrackId, c.Plays)), Is.EquivalentTo(new[] { ("single", 2), ("album", 1) }));
    }

    [Test]
    public async Task GetCountsAsync_ShowsTheGivenTracksTitleAndArtists()
    {
        await _store.SaveAsync([Play(1, "single", "new rules", "DUA LIPA")]);

        var count = (await _store.GetCountsAsync(10, tracks: [new TrackMatch("album", "New Rules", "Dua Lipa", "Dua Lipa")])).Single();

        Assert.That((count.Name, count.Artists), Is.EqualTo(("New Rules", "Dua Lipa")));
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
