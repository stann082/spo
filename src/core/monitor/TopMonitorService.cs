using core.config;
using core.spotify;
using SpotifyAPI.Web;

namespace core.monitor;

public class TopMonitorService : ITopMonitorService
{

    #region Constructors

    public TopMonitorService(ApplicationConfig config, ISpotifyClientFactory clientFactory, ISnapshotStore store)
    {
        _config = config;
        _clientFactory = clientFactory;
        _store = store;
    }

    #endregion

    #region Variables

    private readonly ApplicationConfig _config;
    private readonly ISpotifyClientFactory _clientFactory;
    private readonly ISnapshotStore _store;

    #endregion

    #region Public Methods

    public async Task<MonitorRunResult> RunAsync(bool persist = true, CancellationToken cancellationToken = default)
    {
        var settings = _config.Monitor;
        int limit = Math.Clamp(settings.Limit, 1, SpotifyLimits.TopItemsMax);
        var ranges = settings.TimeRanges is { Length: > 0 } ? settings.TimeRanges : ["short"];

        // Validate every range before touching Spotify or the store, so a typo fails the run cleanly.
        foreach (var range in ranges)
        {
            SpotifyTimeRange.Parse(range);
        }

        var spotify = _clientFactory.CreateUserClient();
        await _store.InitializeAsync(cancellationToken);

        var capturedAt = DateTime.UtcNow;
        var diffs = new List<TopDiff>();

        foreach (var range in ranges)
        {
            diffs.Add(await CompareListAsync(spotify, TopEntityType.Artist, range, limit, capturedAt, persist, cancellationToken));
            diffs.Add(await CompareListAsync(spotify, TopEntityType.Track, range, limit, capturedAt, persist, cancellationToken));
        }

        int pruned = 0;
        if (persist && settings.RetentionDays > 0)
        {
            pruned = await _store.PruneAsync(capturedAt.AddDays(-settings.RetentionDays), cancellationToken);
        }

        var favorites = string.IsNullOrWhiteSpace(settings.FavoritesPlaylist)
            ? null
            : await SyncFavoritesAsync(spotify, FavoritesPlaylist.Name(settings.FavoritesPlaylist, DateTime.Now), persist, cancellationToken);

        return new MonitorRunResult
        {
            CapturedAt = capturedAt,
            Diffs = diffs,
            PrunedSnapshots = pruned,
            Favorites = favorites
        };
    }

    #endregion

    #region Helper Methods

    private async Task<TopDiff> CompareListAsync(
        ISpotifyClient spotify,
        TopEntityType entityType,
        string range,
        int limit,
        DateTime capturedAt,
        bool persist,
        CancellationToken cancellationToken)
    {
        var current = new TopSnapshot
        {
            CapturedAt = capturedAt,
            EntityType = entityType,
            TimeRange = range,
            Entries = entityType == TopEntityType.Artist
                ? await FetchArtistsAsync(spotify, range, limit, cancellationToken)
                : await FetchTracksAsync(spotify, range, limit, cancellationToken)
        };

        var previous = await _store.GetLatestAsync(entityType, range, cancellationToken);
        var diff = TopDiffCalculator.Compare(previous, current);

        if (persist)
        {
            await _store.SaveAsync(current, cancellationToken);
        }

        return diff;
    }

    /// <summary>
    /// Makes the favorites playlist equal to the top tracks of the last 4 weeks, in rank order,
    /// creating it if this year's does not exist yet. A failure is reported in the result rather
    /// than thrown: the snapshots are already saved and the report should still go out.
    /// </summary>
    private static async Task<FavoritesSyncResult> SyncFavoritesAsync(ISpotifyClient spotify, string name, bool persist, CancellationToken cancellationToken)
    {
        try
        {
            var top = await FetchTracksAsync(spotify, "short", SpotifyLimits.TopItemsMax, cancellationToken);
            var desired = top.Select(t => $"spotify:track:{t.SpotifyId}").ToList();

            var me = await spotify.UserProfile.Current(cancellationToken);
            var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 }, cancellationToken);
            var matches = (await spotify.PaginateAll(page))
                .Where(p => p.Owner?.Id == me.Id && string.Equals(p.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 1)
            {
                throw new SpoException($"{matches.Count} of your playlists are named '{name}'. Rename or delete the extras.");
            }

            var playlist = matches.SingleOrDefault();
            var current = new List<string>();
            if (playlist != null)
            {
                var itemsPage = await spotify.Playlists.GetItems(playlist.Id, cancellationToken);
                current = (await spotify.PaginateAll(itemsPage))
                    .Select(item => item.Track switch
                    {
                        FullTrack track => track.Uri,
                        FullEpisode episode => episode.Uri,
                        _ => null
                    })
                    .Where(uri => uri != null)
                    .ToList();
            }

            var plan = FavoritesPlaylist.Plan(current, desired);
            var result = new FavoritesSyncResult
            {
                PlaylistName = name,
                Created = playlist == null,
                Plan = plan,
                TrackCount = desired.Count,
                DryRun = !persist
            };

            if (!persist || (playlist != null && plan.IsUnchanged))
            {
                return result;
            }

            var playlistId = playlist?.Id;
            if (playlistId == null)
            {
                var request = new PlaylistCreateRequest(name)
                {
                    Public = true,
                    Description = "Your top tracks of the last 4 weeks, kept up to date daily by spo."
                };
                playlistId = (await spotify.Playlists.Create(me.Id, request, cancellationToken)).Id;
            }

            // Replace takes one request's worth; anything past that is appended in order.
            var first = desired.Take(SpotifyLimits.PlaylistItemsPerRequest).ToList();
            await spotify.Playlists.ReplaceItems(playlistId, new PlaylistReplaceItemsRequest(first), cancellationToken);
            await Batching.ForEachChunkAsync(desired.Skip(first.Count).ToList(), SpotifyLimits.PlaylistItemsPerRequest, chunk =>
                spotify.Playlists.AddItems(playlistId, new PlaylistAddItemsRequest(chunk), cancellationToken));

            return result;
        }
        catch (Exception ex) when (ex is APIException or SpoException or HttpRequestException)
        {
            return new FavoritesSyncResult { PlaylistName = name, DryRun = !persist, Error = ex.Message };
        }
    }

    private static async Task<List<TopEntry>> FetchArtistsAsync(ISpotifyClient spotify, string range, int limit, CancellationToken cancellationToken)
    {
        var request = new PersonalizationTopRequest { Limit = limit, TimeRangeParam = SpotifyTimeRange.Parse(range) };
        var result = await spotify.Personalization.GetTopArtists(request, cancellationToken);

        int rank = 1;
        return (result.Items ?? [])
            .Select(a => new TopEntry(rank++, a.Id, a.Name, string.Join(", ", a.Genres.Take(2))))
            .ToList();
    }

    private static async Task<List<TopEntry>> FetchTracksAsync(ISpotifyClient spotify, string range, int limit, CancellationToken cancellationToken)
    {
        var request = new PersonalizationTopRequest { Limit = limit, TimeRangeParam = SpotifyTimeRange.Parse(range) };
        var result = await spotify.Personalization.GetTopTracks(request, cancellationToken);

        int rank = 1;
        return (result.Items ?? [])
            .Select(t => new TopEntry(rank++, t.Id, t.Name, string.Join(", ", t.Artists.Select(a => a.Name))))
            .ToList();
    }

    #endregion

}
