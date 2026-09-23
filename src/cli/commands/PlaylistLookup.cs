using core;
using core.playlists;
using SpotifyAPI.Web;

namespace cli.commands;

public static class PlaylistLookup
{

    #region Public Methods

    /// <summary>
    /// The one playlist whose id or exact name (case-insensitive) matches, among the user's
    /// playlists. Refuses to guess when a name is shared by several.
    /// </summary>
    public static SimplePlaylist Find(IEnumerable<SimplePlaylist> playlists, string nameOrId)
    {
        var wanted = nameOrId.Trim();
        var matches = playlists
            .Where(p => p.Id == wanted || string.Equals(p.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new SpoException($"No playlist named '{wanted}'. Use its exact name or id."),
            _ => throw new SpoException($"{matches.Count} playlists are named '{wanted}'. Use the id instead: {string.Join(", ", matches.Select(p => p.Id))}")
        };
    }

    /// <summary>Changing a playlist's tracks or details needs the user to own it.</summary>
    public static void RequireOwned(SimplePlaylist playlist, string userId, string action)
    {
        if (playlist.Owner?.Id != userId)
        {
            throw new SpoException($"'{playlist.Name}' belongs to {playlist.Owner?.DisplayName ?? "someone else"}. Only playlists you own can be {action}.");
        }
    }

    /// <summary>Everything in a playlist that can be moved or removed by uri: tracks and podcast episodes.</summary>
    public static async Task<List<SourceItem>> GetItemsAsync(ISpotifyClient spotify, string playlistId)
    {
        var itemsPage = await spotify.Playlists.GetItems(playlistId);
        return (await spotify.PaginateAll(itemsPage))
            .Select(item => item.Track switch
            {
                FullTrack track => new SourceItem(track.Uri, $"{track.Name} — {string.Join(", ", track.Artists.Select(a => a.Name))}"),
                FullEpisode episode => new SourceItem(episode.Uri, $"{episode.Name} (podcast episode)"),
                _ => null
            })
            .Where(item => item != null)
            .ToList();
    }

    #endregion

}
