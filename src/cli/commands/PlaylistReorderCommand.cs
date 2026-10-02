using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Reorders an existing playlist from a file: either into the order the file lists, or by
/// pulling apart tracks by the same artist that sit back to back. Tracks are moved one at a
/// time, so nothing is removed and every track keeps its date added.
/// </summary>
public static class PlaylistReorderCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        var definition = ReorderDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlist = PlaylistLookup.Find(await spotify.PaginateAll(page), definition.Name);
        PlaylistLookup.RequireOwned(playlist, me.Id, "reordered");

        var items = await GetEntriesAsync(spotify, playlist);

        var order = definition.SpreadArtists
            ? ArtistSpread.Arrange(items)
            : ReorderPlan.ResolveOrder(definition, playlist.Name, items);
        var plan = ReorderPlan.Build(order);

        int clashesBefore = ArtistSpread.CountClashes(items, Enumerable.Range(0, items.Count).ToList());
        int clashesAfter = ArtistSpread.CountClashes(items, order);

        if (plan.Moves.Count == 0)
        {
            Console.WriteLine(definition.SpreadArtists && clashesAfter > 0
                ? $"'{playlist.Name}' ({items.Count} tracks): {clashesAfter} same-artist pair(s) back to back that cannot be pulled apart."
                : $"'{playlist.Name}' ({items.Count} tracks) is already in that order.");
            return 0;
        }

        Console.WriteLine($"Reordering '{playlist.Name}' ({items.Count} tracks): {plan.Moves.Count} to move, {items.Count - plan.Moves.Count} stay where they are.");

        var newPosition = new int[items.Count];
        for (int i = 0; i < order.Count; i++)
        {
            newPosition[order[i]] = i;
        }

        foreach (var move in plan.Moves.OrderBy(m => newPosition[m.Item]))
        {
            Console.WriteLine($"  #{move.Item + 1} -> #{newPosition[move.Item] + 1}  {items[move.Item].Display}");
        }

        Console.WriteLine($"Same artist back to back: {clashesBefore} pair(s) now, {clashesAfter} afterwards.");
        Console.WriteLine();

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        // Each move is made against the snapshot the one before it produced.
        string snapshot = null;
        foreach (var move in plan.Moves)
        {
            var request = new PlaylistReorderItemsRequest(move.From, move.InsertBefore);
            if (snapshot != null)
            {
                request.SnapshotId = snapshot;
            }

            snapshot = (await spotify.Playlists.ReorderItems(playlist.Id, request)).SnapshotId;
        }

        ConsoleWrapper.WriteSuccess($"Moved {plan.Moves.Count} track(s) in '{playlist.Name}'.");
        return 0;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Every item with its position intact: a move is by position, so an item that cannot be
    /// read cannot simply be skipped the way the other commands do.
    /// </summary>
    private static async Task<List<PlaylistEntry>> GetEntriesAsync(ISpotifyClient spotify, SimplePlaylist playlist)
    {
        var itemsPage = await spotify.Playlists.GetItems(playlist.Id);
        var entries = (await spotify.PaginateAll(itemsPage))
            .Select(item => item.Track switch
            {
                FullTrack track => new PlaylistEntry(
                    track.Uri,
                    $"{track.Name} — {string.Join(", ", track.Artists.Select(a => a.Name))}",
                    track.Artists.Select(a => a.Name).ToList()),
                FullEpisode episode => new PlaylistEntry(episode.Uri, $"{episode.Name} (podcast episode)", []),
                _ => null
            })
            .ToList();

        var unreadable = entries
            .Select((entry, index) => (entry, index))
            .Where(x => x.entry == null)
            .Select(x => x.index == 0 ? "#1" : $"#{x.index + 1} (after {entries[x.index - 1]?.Display ?? "another one"})")
            .ToList();

        if (unreadable.Count > 0)
        {
            throw new SpoException($"'{playlist.Name}' has {unreadable.Count} item(s) Spotify gives no details for, so positions cannot be worked out. Remove them in Spotify first: {string.Join("; ", unreadable)}");
        }

        return entries;
    }

    #endregion

}
