using Newtonsoft.Json;

namespace core.playlists;

/// <summary>How a track in a playlist file gets onto the playlist.</summary>
public enum TrackSource
{
    /// <summary>Has an id: added directly.</summary>
    Id,

    /// <summary>Has a title (and usually an artist): looked up with Spotify search.</summary>
    Search,

    /// <summary>Has a status such as "blocked": not added, reported under that status.</summary>
    Skipped
}

public class TrackDefinition
{

    #region Properties

    /// <summary>A bare track id or a full spotify:track: URI.</summary>
    public string Id { get; set; }

    public string Title { get; set; }

    public string Artist { get; set; }

    /// <summary>Any value marks the track as skipped. An id takes precedence over a status.</summary>
    public string Status { get; set; }

    [JsonIgnore]
    public TrackSource Source =>
        !string.IsNullOrWhiteSpace(Id) ? TrackSource.Id
        : !string.IsNullOrWhiteSpace(Status) ? TrackSource.Skipped
        : TrackSource.Search;

    [JsonIgnore]
    public string Uri
    {
        get
        {
            var id = Id?.Trim();
            return id != null && id.StartsWith("spotify:track:", StringComparison.Ordinal) ? id : $"spotify:track:{id}";
        }
    }

    [JsonIgnore]
    public string SearchQuery => string.IsNullOrWhiteSpace(Artist)
        ? $"track:{Title}"
        : $"track:{Title} artist:{Artist}";

    #endregion

    #region Public Methods

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return Id;
        }

        return string.IsNullOrWhiteSpace(Artist) ? Title : $"{Title} — {Artist}";
    }

    #endregion

}
