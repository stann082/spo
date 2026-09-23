using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Adds tracks to an existing playlist from the same kind of file --create takes; "name" picks the
/// playlist. Tracks it already has are skipped, so adding the same file twice is harmless.
/// </summary>
public static class PlaylistAddCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        var definition = PlaylistDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var target = PlaylistLookup.Find(await spotify.PaginateAll(page), definition.Name);
        PlaylistLookup.RequireOwned(target, me.Id, "added to");

        var itemsPage = await spotify.Playlists.GetItems(target.Id);
        var existing = (await spotify.PaginateAll(itemsPage))
            .Select(item => item.Track switch
            {
                FullTrack track => track.Uri,
                FullEpisode episode => episode.Uri,
                _ => null
            })
            .Where(uri => uri != null)
            .ToList();

        var result = await TrackFileResolver.ResolveAsync(spotify, definition);
        var selection = result.ExcludeExisting(existing);
        var uris = selection.ToAdd.Select(t => t.Uri).ToList();

        if (dryRun)
        {
            Console.WriteLine($"Would add {uris.Count} track(s) to '{target.Name}' ({existing.Count} there now).");
        }
        else
        {
            await Batching.ForEachChunkAsync(uris, SpotifyLimits.PlaylistItemsPerRequest, chunk =>
                spotify.Playlists.AddItems(target.Id, new PlaylistAddItemsRequest(chunk)));

            ConsoleWrapper.WriteSuccess($"Added {uris.Count} track(s) to '{target.Name}' ({existing.Count + uris.Count} there now).");
        }

        if (selection.AlreadyThere.Count > 0)
        {
            Console.WriteLine("Already in the playlist:");
            foreach (var track in selection.AlreadyThere)
            {
                Console.WriteLine($"  {track.Track}");
            }
        }

        TrackFileResolver.ReportLeftovers(result);

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
        }

        return 0;
    }

    #endregion

}
