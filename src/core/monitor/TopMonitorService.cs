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

        return new MonitorRunResult
        {
            CapturedAt = capturedAt,
            Diffs = diffs,
            PrunedSnapshots = pruned
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
