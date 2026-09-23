using core.spotify;
using SpotifyAPI.Web;

namespace core.export;

public record ArtistRecord(string Id, string Name);

/// <param name="AlbumType">"album", "single" or "compilation". A compilation's release date is
/// usually much later than the song's.</param>
/// <param name="ReleaseDate">As Spotify gives it: YYYY, YYYY-MM or YYYY-MM-DD.</param>
public record AlbumRecord(string Id, string Name, string AlbumType, string ReleaseDate, string Year);

/// <summary>A track as spo exports it. Optional fields are left out when not known or not asked for.</summary>
public class TrackRecord
{

    #region Properties

    public string Id { get; init; }

    public string Uri { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<ArtistRecord> Artists { get; init; } = [];

    public AlbumRecord Album { get; init; }

    public int DurationMs { get; init; }

    public bool Explicit { get; init; }

    /// <summary>International Standard Recording Code, when Spotify has one.</summary>
    public string Isrc { get; init; }

    /// <summary>When the track was added to the playlist (playlist exports only).</summary>
    public DateTime? AddedAt { get; init; }

    /// <summary>When the track was played (listening-history exports only).</summary>
    public DateTime? PlayedAt { get; init; }

    /// <summary>Merged genres of the track's artists; only present when genres were requested.</summary>
    public IReadOnlyList<string> Genres { get; init; }

    #endregion

    #region Public Methods

    public static TrackRecord From(
        FullTrack track,
        DateTime? addedAt = null,
        DateTime? playedAt = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>> genresByArtist = null)
    {
        var album = track.Album;
        string isrc = null;
        track.ExternalIds?.TryGetValue("isrc", out isrc);

        return new TrackRecord
        {
            Id = track.Id,
            Uri = track.Uri,
            Name = track.Name,
            Artists = (track.Artists ?? []).Select(a => new ArtistRecord(a.Id, a.Name)).ToList(),
            Album = album == null
                ? null
                : new AlbumRecord(album.Id, album.Name, album.AlbumType, album.ReleaseDate, NullIfEmpty(ReleaseDate.Year(album.ReleaseDate))),
            DurationMs = track.DurationMs,
            Explicit = track.Explicit,
            Isrc = NullIfEmpty(isrc),
            AddedAt = addedAt,
            PlayedAt = playedAt,
            Genres = genresByArtist == null
                ? null
                : ArtistGenres.ForTrack((track.Artists ?? []).Select(a => a.Id), genresByArtist)
        };
    }

    #endregion

    #region Helper Methods

    private static string NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    #endregion

}
