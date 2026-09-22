using SpotifyAPI.Web;

namespace core.spotify;

public static class SpotifyScopes
{

    /// <summary>
    /// What spo asks for at login: reading your listening data, and reading and editing your
    /// library and playlists. Add to this list when a command needs more, rather than requesting
    /// every scope Spotify has.
    /// </summary>
    public static readonly IReadOnlyList<string> Default =
    [
        Scopes.UserTopRead,
        Scopes.UserReadRecentlyPlayed,
        Scopes.UserLibraryRead,
        Scopes.UserLibraryModify,
        Scopes.PlaylistReadPrivate,
        Scopes.PlaylistReadCollaborative,
        Scopes.PlaylistModifyPrivate,
        Scopes.PlaylistModifyPublic
    ];

}
