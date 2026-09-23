using SpotifyAPI.Web;

namespace core.spotify;

/// <summary>
/// Spotify only tags artists with genres, never tracks, and the artists embedded in playlist items
/// carry no genres at all. So a track's genres are looked up through its artists.
/// </summary>
public static class ArtistGenres
{

    #region Public Methods

    /// <summary>
    /// Genres for every distinct artist id, looked up <see cref="SpotifyLimits.ArtistsPerRequest"/>
    /// at a time. Artists Spotify does not return map to no genres.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> FetchAsync(
        ISpotifyClient spotify,
        IEnumerable<string> artistIds,
        CancellationToken cancellationToken = default)
    {
        var genres = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var distinctIds = artistIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal);

        await Batching.ForEachChunkAsync(distinctIds, SpotifyLimits.ArtistsPerRequest, async chunk =>
        {
            var response = await spotify.Artists.GetSeveral(new ArtistsRequest(chunk), cancellationToken);
            foreach (var artist in response.Artists.Where(a => a != null))
            {
                genres[artist.Id] = artist.Genres ?? [];
            }
        }, cancellationToken);

        return genres;
    }

    /// <summary>
    /// The genres of every artist credited on a track, first artist first, without repeats.
    /// </summary>
    public static IReadOnlyList<string> ForTrack(IEnumerable<string> trackArtistIds, IReadOnlyDictionary<string, IReadOnlyList<string>> genresByArtist)
    {
        return trackArtistIds
            .Where(id => !string.IsNullOrEmpty(id))
            .SelectMany(id => genresByArtist.TryGetValue(id, out var genres) ? genres : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    #endregion

}
