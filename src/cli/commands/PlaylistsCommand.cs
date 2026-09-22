using cli.options;
using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

public static class PlaylistsCommand
{

    #region Constants

    /// <summary>Searches in flight at once when resolving a playlist file.</summary>
    private const int SearchConcurrency = 5;

    #endregion

    #region Public Methods

    public static Task<int> ExecuteAsync(PlaylistsOptions options, ISpotifyClientFactory clientFactory)
    {
        return !string.IsNullOrEmpty(options.CreateFile)
            ? CreateAsync(options.CreateFile, clientFactory)
            : ListAsync(options, clientFactory);
    }

    #endregion

    #region Helper Methods

    private static async Task<int> CreateAsync(string path, ISpotifyClientFactory clientFactory)
    {
        // Validate the file before logging in or creating anything.
        var definition = PlaylistDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        // Search first, create second: a failure part-way through leaves no empty playlist behind.
        var toSearch = definition.Tracks.Where(t => t.Source == TrackSource.Search).ToList();
        if (toSearch.Count > 0)
        {
            Console.WriteLine($"Searching for {toSearch.Count} track(s)...");
        }

        var hits = await Batching.MapAsync(toSearch, SearchConcurrency, async (track, token) =>
        {
            var response = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, track.SearchQuery), token);
            return response.Tracks?.Items?.FirstOrDefault()?.Uri;
        });

        var searchHits = toSearch
            .Zip(hits)
            .ToDictionary(pair => pair.First, pair => pair.Second);
        var result = PlaylistImportResult.Assemble(definition, searchHits);

        var me = await spotify.UserProfile.Current();
        var createRequest = new PlaylistCreateRequest(definition.Name);
        if (!string.IsNullOrEmpty(definition.Description))
        {
            createRequest.Description = definition.Description;
        }

        var playlist = await spotify.Playlists.Create(me.Id, createRequest);
        ConsoleWrapper.WriteSuccess($"Created playlist \"{definition.Name}\".");

        await Batching.ForEachChunkAsync(result.Uris, SpotifyLimits.PlaylistItemsPerRequest, chunk =>
            spotify.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(chunk)));

        Console.WriteLine($"Added {result.Uris.Count} of {result.Requested} track(s).");

        if (result.NotFound.Count > 0)
        {
            Console.WriteLine("Could not find the following track(s):");
            foreach (var track in result.NotFound)
            {
                Console.WriteLine($"  {track}");
            }
        }

        foreach (var group in result.Skipped)
        {
            Console.WriteLine($"Skipped ({group.Key}):");
            foreach (var track in group)
            {
                Console.WriteLine($"  {track}");
            }
        }

        return 0;
    }

    private static async Task<int> ListAsync(PlaylistsOptions options, ISpotifyClientFactory clientFactory)
    {
        if (options.ShowTrackId && !options.ShowTracks)
        {
            throw new SpoException("--show-track-id only applies together with --show-tracks.");
        }

        var spotify = clientFactory.CreateUserClient();

        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        IEnumerable<SimplePlaylist> playlists = await spotify.PaginateAll(page);
        if (!string.IsNullOrEmpty(options.Query))
        {
            playlists = playlists.Where(p => p.Name.Contains(options.Query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var playlist in playlists)
        {
            if (!options.ShowTracks)
            {
                Console.WriteLine(playlist.Name);
                continue;
            }

            var itemsPage = await spotify.Playlists.GetItems(playlist.Id);
            foreach (var item in await spotify.PaginateAll(itemsPage))
            {
                if (item.Track is not FullTrack track)
                {
                    continue;
                }

                var line = $"[{track.Name}],[{string.Join(", ", track.Artists.Select(a => a.Name))}]";
                Console.WriteLine(options.ShowTrackId ? $"{line},[{track.Id}]" : line);
            }
        }

        return 0;
    }

    #endregion

}
