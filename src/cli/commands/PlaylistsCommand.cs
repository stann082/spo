using cli.options;
using core;
using core.export;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

public static class PlaylistsCommand
{

    #region Public Methods

    public static Task<int> ExecuteAsync(PlaylistsOptions options, ISpotifyClientFactory clientFactory)
    {
        if (!string.IsNullOrEmpty(options.CreateFile))
        {
            return CreateAsync(options.CreateFile, options.DryRun, clientFactory);
        }

        if (!string.IsNullOrEmpty(options.AddFile))
        {
            return PlaylistAddCommand.ExecuteAsync(options.AddFile, options.DryRun, clientFactory);
        }

        if (!string.IsNullOrEmpty(options.SplitFile))
        {
            return PlaylistSplitCommand.ExecuteAsync(options.SplitFile, options.DryRun, options.MarkOrphans, clientFactory);
        }

        if (!string.IsNullOrEmpty(options.MoveFile))
        {
            return PlaylistMoveCommand.ExecuteAsync(options.MoveFile, options.DryRun, clientFactory);
        }

        if (!string.IsNullOrEmpty(options.RemoveFile))
        {
            return PlaylistRemoveCommand.ExecuteAsync(options.RemoveFile, options.DryRun, clientFactory);
        }

        if (!string.IsNullOrEmpty(options.DescribeFile))
        {
            return PlaylistDescribeCommand.ExecuteAsync(options.DescribeFile, options.DryRun, clientFactory);
        }

        if (options.DryRun)
        {
            throw new SpoException("--dry-run only applies to --create, --add, --split, --move, --remove and --describe; listing never changes anything.");
        }

        return ListAsync(options, clientFactory);
    }

    #endregion

    #region Helper Methods

    private static async Task<int> CreateAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        // Validate the file before logging in or creating anything.
        var definition = PlaylistDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        // Search first, create second: a failure part-way through leaves no empty playlist behind.
        var result = await TrackFileResolver.ResolveAsync(spotify, definition);

        if (dryRun)
        {
            Console.WriteLine($"Would create playlist \"{definition.Name}\" with {result.Uris.Count} of {result.Requested} track(s).");
        }
        else
        {
            var me = await spotify.UserProfile.Current();
            var createRequest = new PlaylistCreateRequest(definition.Name) { Public = true };
            if (!string.IsNullOrEmpty(definition.Description))
            {
                createRequest.Description = definition.Description;
            }

            var playlist = await spotify.Playlists.Create(me.Id, createRequest);
            ConsoleWrapper.WriteSuccess($"Created playlist \"{definition.Name}\".");

            await Batching.ForEachChunkAsync(result.Uris, SpotifyLimits.PlaylistItemsPerRequest, chunk =>
                spotify.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(chunk)));

            Console.WriteLine($"Added {result.Uris.Count} of {result.Requested} track(s).");
        }

        TrackFileResolver.ReportLeftovers(result);

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
        }

        return 0;
    }

    private static async Task<int> ListAsync(PlaylistsOptions options, ISpotifyClientFactory clientFactory)
    {
        if (options.ShowTrackId && !options.ShowTracks)
        {
            throw new SpoException("--show-track-id only applies together with --show-tracks.");
        }

        if (options.ShowGenres && !options.ShowTracks)
        {
            throw new SpoException("--show-genres only applies together with --show-tracks.");
        }

        bool json = OutputFormat.Parse(options.Format, "text", "json") == "json";

        var spotify = clientFactory.CreateUserClient();

        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        IEnumerable<SimplePlaylist> matches = await spotify.PaginateAll(page);
        if (!string.IsNullOrEmpty(options.Query))
        {
            matches = matches.Where(p => p.Name.Contains(options.Query, StringComparison.OrdinalIgnoreCase));
        }

        var playlists = matches.ToList();

        if (!options.ShowTracks)
        {
            if (json)
            {
                Console.WriteLine(JsonOutput.Serialize(playlists.Select(p => PlaylistRecord.From(p))));
                return 0;
            }

            foreach (var playlist in playlists)
            {
                Console.WriteLine(playlist.Name);
            }

            return 0;
        }

        var itemsByPlaylist = new List<(SimplePlaylist playlist, List<PlaylistTrack<IPlayableItem>> items)>();
        foreach (var playlist in playlists)
        {
            var itemsPage = await spotify.Playlists.GetItems(playlist.Id);
            var items = (await spotify.PaginateAll(itemsPage)).Where(item => item.Track is FullTrack).ToList();
            itemsByPlaylist.Add((playlist, items));
        }

        var tracks = itemsByPlaylist.SelectMany(x => x.items).Select(item => (FullTrack)item.Track).ToList();

        // Genres need every artist up front, so they can be looked up in a few batched calls
        // rather than one call per track.
        var genresByArtist = options.ShowGenres
            ? await ArtistGenres.FetchAsync(spotify, tracks.SelectMany(t => t.Artists.Select(a => a.Id)))
            : null;

        if (json)
        {
            var records = itemsByPlaylist.Select(x => PlaylistRecord.From(
                x.playlist,
                x.items.Select(item => TrackRecord.From((FullTrack)item.Track, addedAt: item.AddedAt, genresByArtist: genresByArtist)).ToList()));
            Console.WriteLine(JsonOutput.Serialize(records));
            return 0;
        }

        foreach (var track in tracks)
        {
            Console.WriteLine(FormatTrack(track, genresByArtist, options.ShowTrackId));
        }

        return 0;
    }

    private static string FormatTrack(FullTrack track, IReadOnlyDictionary<string, IReadOnlyList<string>> genresByArtist, bool showTrackId)
    {
        var artists = string.Join(", ", track.Artists.Select(a => a.Name));
        var line = $"[{track.Name}],[{artists}],[{track.Album?.Name}],[{ReleaseDate.Year(track.Album?.ReleaseDate)}]";

        if (genresByArtist != null)
        {
            var genres = ArtistGenres.ForTrack(track.Artists.Select(a => a.Id), genresByArtist);
            line += $",[{string.Join(", ", genres)}]";
        }

        return showTrackId ? $"{line},[{track.Id}]" : line;
    }

    #endregion

}
