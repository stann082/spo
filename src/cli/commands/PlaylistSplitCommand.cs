using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Moves tracks out of one playlist into new ones. For each new playlist the tracks are added
/// first and only then removed from the source, so a failure part-way can leave a track in two
/// places but never in none.
/// </summary>
public static class PlaylistSplitCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, bool markOrphans, ISpotifyClientFactory clientFactory)
    {
        var definition = SplitDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlists = await spotify.PaginateAll(page);

        var source = PlaylistLookup.Find(playlists, definition.Source);
        PlaylistLookup.RequireOwned(source, me.Id, "split");

        var sourceItems = await PlaylistLookup.GetItemsAsync(spotify, source.Id);

        var plan = SplitPlan.Build(definition, source.Name, sourceItems, playlists.Select(p => p.Name));

        PrintPlan(source.Name, sourceItems.Count, plan, markOrphans);
        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in plan.Targets)
        {
            await MoveAsync(spotify, me.Id, source, target, removed);
        }

        if (plan.Orphans.Count == 0)
        {
            ConsoleWrapper.WriteSuccess($"'{source.Name}' is now empty. Delete it in Spotify if you no longer need it.");
            return 0;
        }

        if (markOrphans)
        {
            var orphanedName = SplitPlan.OrphanedName(source.Name);
            await spotify.Playlists.ChangeDetails(source.Id, new PlaylistChangeDetailsRequest { Name = orphanedName, Public = false });
            ConsoleWrapper.WriteSuccess($"Renamed '{source.Name}' to '{orphanedName}' and made it private.");
        }
        else
        {
            Console.WriteLine($"{plan.Orphans.Count} track(s) stay in '{source.Name}'.");
        }

        return 0;
    }

    #endregion

    #region Helper Methods

    private static void PrintPlan(string sourceName, int sourceCount, SplitPlan plan, bool markOrphans)
    {
        Console.WriteLine($"Splitting '{sourceName}' ({sourceCount} tracks):");
        foreach (var target in plan.Targets)
        {
            Console.WriteLine($"  -> '{target.Definition.Name}': {target.Uris.Count} track(s)");
        }

        Console.WriteLine($"  Staying in '{sourceName}': {plan.Orphans.Count} track(s)");
        foreach (var orphan in plan.Orphans)
        {
            Console.WriteLine($"       {orphan.Display}");
        }

        if (markOrphans && plan.Orphans.Count > 0)
        {
            Console.WriteLine($"  Then rename '{sourceName}' to '{SplitPlan.OrphanedName(sourceName)}' and make it private.");
        }

        Console.WriteLine();
    }

    private static async Task MoveAsync(ISpotifyClient spotify, string userId, SimplePlaylist source, SplitTarget target, HashSet<string> removed)
    {
        var request = new PlaylistCreateRequest(target.Definition.Name) { Public = true };
        if (!string.IsNullOrEmpty(target.Definition.Description))
        {
            request.Description = target.Definition.Description;
        }

        var created = await spotify.Playlists.Create(userId, request);

        await Batching.ForEachChunkAsync(target.Uris, SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.AddItems(created.Id, new PlaylistAddItemsRequest(chunk)));

        // Only now that every add succeeded do the tracks leave the source. A track listed under
        // two new playlists is removed once, after its first move.
        var toRemove = target.Uris.Where(uri => !removed.Contains(uri)).ToList();
        await Batching.ForEachChunkAsync(toRemove, SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.RemoveItems(source.Id, new PlaylistRemoveItemsRequest
            {
                Tracks = chunk.Select(uri => new PlaylistRemoveItemsRequest.Item { Uri = uri }).ToList()
            }));

        removed.UnionWith(toRemove);
        ConsoleWrapper.WriteSuccess($"Moved {target.Uris.Count} track(s) to '{target.Definition.Name}'.");
    }

    #endregion

}
