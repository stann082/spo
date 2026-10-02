using core.spotify;
using SpotifyAPI.Web;

namespace core.plays;

/// <summary>
/// Copies Spotify's recently-played list into the play log. Spotify keeps no play counts and
/// shows only the last fifty plays, so the log is built by polling more often than fifty tracks
/// can go by.
/// </summary>
public class PlayRecorder
{

    #region Constructors

    public PlayRecorder(ISpotifyClientFactory clientFactory, IPlayStore store)
    {
        _clientFactory = clientFactory;
        _store = store;
    }

    #endregion

    #region Variables

    private readonly ISpotifyClientFactory _clientFactory;
    private readonly IPlayStore _store;

    #endregion

    #region Public Methods

    public async Task<PlayLogResult> RecordAsync(bool persist = true, CancellationToken cancellationToken = default)
    {
        var spotify = _clientFactory.CreateUserClient();
        await _store.InitializeAsync(cancellationToken);

        var request = new PlayerRecentlyPlayedRequest { Limit = SpotifyLimits.RecentlyPlayedMax };
        var paging = await spotify.Player.GetRecentlyPlayed(request, cancellationToken);

        var fetched = (paging.Items ?? [])
            .Where(i => i.Track != null)
            .Select(i => new PlayRecord(
                i.PlayedAt.ToUniversalTime(),
                i.Track.Id,
                i.Track.Name,
                string.Join(", ", i.Track.Artists.Select(a => a.Name))))
            .ToList();

        var latest = await _store.GetLatestPlayedAtAsync(cancellationToken);
        var fresh = PlayLog.SelectNew(latest, fetched);

        if (persist)
        {
            await _store.SaveAsync(fresh, cancellationToken);
        }

        return new PlayLogResult
        {
            Fetched = fetched.Count,
            Added = fresh.Count,
            MayHaveMissedPlays = PlayLog.MayHaveMissedPlays(latest, fetched, SpotifyLimits.RecentlyPlayedMax),
            DryRun = !persist
        };
    }

    #endregion

}
