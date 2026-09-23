using core.monitor;

namespace core.test;

public class FavoritesPlaylistTests
{

    #region Name Tests

    [Test]
    public void Name_PutsTheYearInThePlaceholder()
    {
        Assert.That(FavoritesPlaylist.Name("{year} Favs", new DateTime(2026, 9, 23)), Is.EqualTo("2026 Favs"));
    }

    [Test]
    public void Name_ChangesOnNewYearsDay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FavoritesPlaylist.Name("{year} Favs", new DateTime(2026, 12, 31, 23, 59, 0)), Is.EqualTo("2026 Favs"));
            Assert.That(FavoritesPlaylist.Name("{year} Favs", new DateTime(2027, 1, 1)), Is.EqualTo("2027 Favs"));
        });
    }

    [Test]
    public void Name_WithoutAPlaceholderIsUsedAsIs()
    {
        Assert.That(FavoritesPlaylist.Name("Top Rotation ", new DateTime(2026, 9, 23)), Is.EqualTo("Top Rotation"));
    }

    #endregion

    #region Plan Tests

    [Test]
    public void Plan_EmptyPlaylistAddsEverythingInRankOrder()
    {
        var plan = FavoritesPlaylist.Plan([], ["a", "b", "c"]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Added, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(plan.Removed, Is.Empty);
            Assert.That(plan.Reordered, Is.False);
            Assert.That(plan.IsUnchanged, Is.False);
        });
    }

    [Test]
    public void Plan_SameTracksInTheSameOrderIsUnchanged()
    {
        Assert.That(FavoritesPlaylist.Plan(["a", "b", "c"], ["a", "b", "c"]).IsUnchanged, Is.True);
    }

    [Test]
    public void Plan_ReportsEntriesAndExitsWithoutCallingThemAReorder()
    {
        var plan = FavoritesPlaylist.Plan(["a", "b", "c"], ["a", "c", "d"]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Added, Is.EqualTo(new[] { "d" }));
            Assert.That(plan.Removed, Is.EqualTo(new[] { "b" }));
            Assert.That(plan.Reordered, Is.False);
        });
    }

    [Test]
    public void Plan_NoticesARankSwap()
    {
        var plan = FavoritesPlaylist.Plan(["a", "b", "c"], ["b", "a", "c"]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Added, Is.Empty);
            Assert.That(plan.Removed, Is.Empty);
            Assert.That(plan.Reordered, Is.True);
        });
    }

    [Test]
    public void Plan_RemovesTracksAddedToThePlaylistByHand()
    {
        var plan = FavoritesPlaylist.Plan(["a", "spotify:local:x", "b"], ["a", "b"]);

        Assert.That(plan.Removed, Is.EqualTo(new[] { "spotify:local:x" }));
    }

    #endregion

    #region Describe Tests

    [Test]
    public void Describe_SummarisesEachOutcome()
    {
        var unchanged = new FavoritesPlan([], [], false);
        var changed = new FavoritesPlan(["d"], ["b"], true);

        Assert.Multiple(() =>
        {
            Assert.That(new FavoritesSyncResult { PlaylistName = "2026 Favs", Created = true, Plan = changed, TrackCount = 50 }.Describe(),
                Is.EqualTo("Playlist '2026 Favs' created with your top 50 tracks."));
            Assert.That(new FavoritesSyncResult { PlaylistName = "2026 Favs", Created = true, Plan = changed, TrackCount = 50, DryRun = true }.Describe(),
                Is.EqualTo("Playlist '2026 Favs' would be created with your top 50 tracks."));
            Assert.That(new FavoritesSyncResult { PlaylistName = "2026 Favs", Plan = unchanged, TrackCount = 50 }.Describe(),
                Is.EqualTo("Playlist '2026 Favs' already matches your top 50 tracks."));
            Assert.That(new FavoritesSyncResult { PlaylistName = "2026 Favs", Plan = changed, TrackCount = 50 }.Describe(),
                Is.EqualTo("Playlist '2026 Favs' updated: 1 added, 1 removed, reordered (50 tracks)."));
            Assert.That(new FavoritesSyncResult { PlaylistName = "2026 Favs", Error = "Service unavailable" }.Describe(),
                Is.EqualTo("Playlist '2026 Favs' was not updated: Service unavailable"));
        });
    }

    [Test]
    public void BuildLines_EndsWithThePlaylistLine()
    {
        var result = new MonitorRunResult
        {
            Favorites = new FavoritesSyncResult { PlaylistName = "2026 Favs", Plan = new FavoritesPlan([], [], false), TrackCount = 50 }
        };

        Assert.That(MonitorReport.BuildLines(result).Last(), Is.EqualTo("Playlist '2026 Favs' already matches your top 50 tracks."));
    }

    #endregion

}
