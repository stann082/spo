using System.Text.RegularExpressions;
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

    #region Constants

    // The favorites playlist shows the year (long range); the rotation playlist shows the month (short range).
    private const string FavoritesDescription = "Your top tracks of the last 12 months, kept up to date daily by spo.";
    private const string RotationDescription = "Your top tracks of the last 4 weeks, kept up to date daily by spo.";

    // Playlists read at once when collecting the user's own copies of songs.
    private const int PlaylistReadConcurrency = 4;

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

        var playlists = new List<(string format, string range, string description)>();
        if (!string.IsNullOrWhiteSpace(settings.FavoritesPlaylist))
        {
            playlists.Add((settings.FavoritesPlaylist, "long", FavoritesDescription));
        }

        if (!string.IsNullOrWhiteSpace(settings.RotationPlaylist))
        {
            playlists.Add((settings.RotationPlaylist, "short", RotationDescription));
        }

        var now = DateTime.Now;
        var names = playlists.Select(p => FavoritesPlaylist.Name(p.format, now)).ToList();
        var favorites = new List<FavoritesSyncResult>();

        // The user's playlists, read once for both: the playlists to sync are found in them, and
        // the tracks in the others are the copies to use.
        Library library = null;
        string libraryError = null;
        if (playlists.Count > 0)
        {
            try
            {
                library = await LoadLibraryAsync(spotify, playlists.Select(p => p.format).ToList(), cancellationToken);
            }
            catch (Exception ex) when (ex is APIException or HttpRequestException)
            {
                libraryError = $"could not read your playlists: {ex.Message}";
            }
        }

        for (int i = 0; i < playlists.Count; i++)
        {
            // Two settings resolving to one name would have the playlists overwrite each other every night.
            if (names.Take(i).Contains(names[i], StringComparer.OrdinalIgnoreCase))
            {
                favorites.Add(new FavoritesSyncResult { PlaylistName = names[i], DryRun = !persist, Error = "FavoritesPlaylist and RotationPlaylist give the same name." });
                continue;
            }

            // Without the library every track would fall back to Spotify's release, so skip the night instead.
            if (libraryError != null)
            {
                favorites.Add(new FavoritesSyncResult { PlaylistName = names[i], DryRun = !persist, Error = libraryError });
                continue;
            }

            favorites.Add(await SyncFavoritesAsync(spotify, library, names[i], playlists[i].range, playlists[i].description, persist, cancellationToken));
        }

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
    /// Makes a playlist equal to the top tracks of <paramref name="range"/>, in rank order,
    /// creating it if this year's does not exist yet. A failure is reported in the result rather
    /// than thrown: the snapshots are already saved and the report should still go out.
    /// </summary>
    private static async Task<FavoritesSyncResult> SyncFavoritesAsync(ISpotifyClient spotify, Library library, string name, string range, string description, bool persist, CancellationToken cancellationToken)
    {
        try
        {
            var topRequest = new PersonalizationTopRequest { Limit = SpotifyLimits.TopItemsMax, TimeRangeParam = SpotifyTimeRange.Parse(range) };
            var top = (await spotify.Personalization.GetTopTracks(topRequest, cancellationToken)).Items ?? [];

            // The user's own copy of each song when they have one; two releases of one song in the
            // top list then become one track.
            int swapped = 0, outside = 0;
            var desired = new List<string>();
            foreach (var track in top)
            {
                var copy = library.Copies.Find(track.Id, track.Name, track.Artists.FirstOrDefault()?.Name);
                if (copy == null) outside++;
                else if (copy != track.Id) swapped++;
                desired.Add($"spotify:track:{copy ?? track.Id}");
            }

            desired = desired.Distinct(StringComparer.Ordinal).ToList();

            var me = library.Me;
            var matches = library.Playlists
                .Where(p => string.Equals(p.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
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
                SwappedForOwnCopy = swapped,
                NotInOtherPlaylists = outside,
                DryRun = !persist
            };

            // Keeps the description true when the range changes; it is only ever set by spo.
            if (persist && playlist != null && playlist.Description != description)
            {
                await spotify.Playlists.ChangeDetails(playlist.Id, new PlaylistChangeDetailsRequest { Description = description }, cancellationToken);
            }

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
                    Description = description
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

    private record Library(PrivateUser Me, IReadOnlyList<SimplePlaylist> Playlists, LibraryCopies Copies);

    /// <summary>
    /// The user's own playlists and the tracks in them. The playlists the monitor keeps, this
    /// year's and earlier ones, are left out of the tracks: they are copies of the top lists.
    /// </summary>
    private static async Task<Library> LoadLibraryAsync(ISpotifyClient spotify, IReadOnlyList<string> managedFormats, CancellationToken cancellationToken)
    {
        var me = await spotify.UserProfile.Current(cancellationToken);
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 }, cancellationToken);
        var owned = (await spotify.PaginateAll(page)).Where(p => p.Owner?.Id == me.Id).ToList();

        var managed = managedFormats
            .Select(format => new Regex(
                "^" + Regex.Escape(format.Trim()).Replace(Regex.Escape(FavoritesPlaylist.YearPlaceholder), @"\d{4}", StringComparison.OrdinalIgnoreCase) + "$",
                RegexOptions.IgnoreCase))
            .ToList();
        var sources = owned.Where(p => !managed.Any(m => m.IsMatch(p.Name?.Trim() ?? string.Empty))).ToList();

        var trackLists = await Batching.MapAsync(sources, PlaylistReadConcurrency, async (playlist, token) =>
        {
            var itemsPage = await spotify.Playlists.GetItems(playlist.Id, token);
            return (await spotify.PaginateAll(itemsPage))
                .Select(item => item.Track as FullTrack)
                .Where(track => !string.IsNullOrEmpty(track?.Id))
                .Select(track => new LibraryTrack(track.Id, track.Name, track.Artists.FirstOrDefault()?.Name))
                .ToList();
        }, cancellationToken);

        return new Library(me, owned, new LibraryCopies(trackLists.SelectMany(tracks => tracks)));
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
