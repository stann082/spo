using SpotifyAPI.Web;

namespace core.export;

/// <summary>A playlist as spo exports it. <see cref="Tracks"/> is only present when tracks were asked for.</summary>
public class PlaylistRecord
{

    #region Properties

    public string Id { get; init; }

    public string Uri { get; init; }

    public string Name { get; init; }

    public string Owner { get; init; }

    public string OwnerId { get; init; }

    public int? TrackCount { get; init; }

    public IReadOnlyList<TrackRecord> Tracks { get; init; }

    #endregion

    #region Public Methods

    public static PlaylistRecord From(SimplePlaylist playlist, IReadOnlyList<TrackRecord> tracks = null)
    {
        return new PlaylistRecord
        {
            Id = playlist.Id,
            Uri = playlist.Uri,
            Name = playlist.Name,
            Owner = playlist.Owner?.DisplayName,
            OwnerId = playlist.Owner?.Id,
            TrackCount = playlist.Tracks?.Total,
            Tracks = tracks
        };
    }

    #endregion

}
