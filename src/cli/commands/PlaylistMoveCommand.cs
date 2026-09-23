using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Moves tracks from one existing playlist to another. Tracks are added to the target first and
/// only then removed from the source, so a failure part-way can leave a track in two places but
/// never in none. Tracks the target already has are just removed from the source.
/// </summary>
public static class PlaylistMoveCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        var definition = MoveDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlists = await spotify.PaginateAll(page);

        var source = PlaylistLookup.Find(playlists, definition.Source);
        var target = PlaylistLookup.Find(playlists, definition.Target);
        if (source.Id == target.Id)
        {
            throw new SpoException($"\"source\" and \"target\" are both '{source.Name}'.");
        }

        PlaylistLookup.RequireOwned(source, me.Id, "moved out of");
        PlaylistLookup.RequireOwned(target, me.Id, "moved into");

        var sourceItems = await PlaylistLookup.GetItemsAsync(spotify, source.Id);
        var targetItems = await PlaylistLookup.GetItemsAsync(spotify, target.Id);

        var plan = MovePlan.Build(definition, source.Name, sourceItems, targetItems.Select(i => i.Uri));

        PrintPlan(source, sourceItems, target, targetItems.Count, plan);
        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        await Batching.ForEachChunkAsync(plan.ToAdd.Select(m => m.Uri).ToList(), SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.AddItems(target.Id, new PlaylistAddItemsRequest(chunk)));

        // Only now that every add succeeded do the tracks leave the source.
        await Batching.ForEachChunkAsync(plan.Moving.Select(m => m.Uri).ToList(), SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.RemoveItems(source.Id, new PlaylistRemoveItemsRequest
            {
                Tracks = chunk.Select(uri => new PlaylistRemoveItemsRequest.Item { Uri = uri }).ToList()
            }));

        ConsoleWrapper.WriteSuccess($"Moved {plan.Moving.Count} track(s) from '{source.Name}' to '{target.Name}'.");
        return 0;
    }

    #endregion

    #region Helper Methods

    private static void PrintPlan(SimplePlaylist source, List<SourceItem> sourceItems, SimplePlaylist target, int targetCount, MovePlan plan)
    {
        // Removing by uri takes every copy of a track, so count what is left rather than subtract.
        var moving = new HashSet<string>(plan.Moving.Select(m => m.Uri), StringComparer.Ordinal);
        var sourceLeft = sourceItems.Count(s => !moving.Contains(s.Uri));

        Console.WriteLine($"Moving {plan.Moving.Count} track(s) from '{source.Name}' ({sourceItems.Count} tracks) to '{target.Name}' ({targetCount} tracks):");
        foreach (var item in plan.ToAdd)
        {
            Console.WriteLine($"  {item.Display}");
        }

        foreach (var item in plan.AlreadyInTarget)
        {
            Console.WriteLine($"  {item.Display} (already in '{target.Name}', only removed from '{source.Name}')");
        }

        Console.WriteLine($"Afterwards: '{source.Name}' {sourceLeft} tracks, '{target.Name}' {targetCount + plan.ToAdd.Count} tracks.");
        Console.WriteLine();
    }

    #endregion

}
