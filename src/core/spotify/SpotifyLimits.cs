namespace core.spotify;

/// <summary>
/// Per-request caps of the Spotify Web API. Chunk with these via <see cref="Batching"/> rather
/// than hardcoding numbers in commands.
/// </summary>
public static class SpotifyLimits
{

    /// <summary>Ids per save/remove call on the user's library (/me/tracks and friends).</summary>
    public const int LibraryItemsPerRequest = 50;

    /// <summary>Items per add/remove call on a playlist.</summary>
    public const int PlaylistItemsPerRequest = 100;

    /// <summary>Largest page Spotify returns for top artists and tracks.</summary>
    public const int TopItemsMax = 50;

    /// <summary>Largest page Spotify returns for recently played tracks.</summary>
    public const int RecentlyPlayedMax = 50;

}
