using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Removes tracks from an existing playlist, by id, from a file in the --create format; "name"
/// picks the playlist. Every copy of a listed track goes.
/// </summary>
public static class PlaylistRemoveCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        var definition = PlaylistDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlist = PlaylistLookup.Find(await spotify.PaginateAll(page), definition.Name);
        PlaylistLookup.RequireOwned(playlist, me.Id, "removed from");

        var items = await PlaylistLookup.GetItemsAsync(spotify, playlist.Id);
        var plan = RemovePlan.Build(definition, playlist.Name, items);

        // Removing by uri takes every copy of a track, so count what is left rather than subtract.
        var removing = new HashSet<string>(plan.Removing.Select(r => r.Uri), StringComparer.Ordinal);
        var left = items.Count(i => !removing.Contains(i.Uri));

        Console.WriteLine($"Removing {plan.Removing.Count} track(s) from '{playlist.Name}' ({items.Count} tracks):");
        foreach (var item in plan.Removing)
        {
            Console.WriteLine($"  {item.Display}");
        }

        Console.WriteLine($"Afterwards: '{playlist.Name}' {left} tracks.");
        Console.WriteLine();

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        await Batching.ForEachChunkAsync(plan.Removing.Select(r => r.Uri).ToList(), SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.RemoveItems(playlist.Id, new PlaylistRemoveItemsRequest
            {
                Tracks = chunk.Select(uri => new PlaylistRemoveItemsRequest.Item { Uri = uri }).ToList()
            }));

        ConsoleWrapper.WriteSuccess($"Removed {plan.Removing.Count} track(s) from '{playlist.Name}' ({left} there now).");
        return 0;
    }

    #endregion

}
