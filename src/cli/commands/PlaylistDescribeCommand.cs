using System.Net;
using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Sets the descriptions of existing playlists from a file. Every playlist is looked up before
/// anything changes, and descriptions that already match are left alone.
/// </summary>
public static class PlaylistDescribeCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(string path, bool dryRun, ISpotifyClientFactory clientFactory)
    {
        var definition = DescribeDefinition.Load(path);
        var spotify = clientFactory.CreateUserClient();

        var me = await spotify.UserProfile.Current();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlists = await spotify.PaginateAll(page);

        // Resolve all of them first, so a typo in the last entry fails before the first one is written.
        var changes = new List<(SimplePlaylist playlist, string current, string wanted)>();
        foreach (var entry in definition.Playlists)
        {
            var playlist = PlaylistLookup.Find(playlists, entry.Name);
            PlaylistLookup.RequireOwned(playlist, me.Id, "described");

            // Spotify hands descriptions back HTML-escaped.
            var current = WebUtility.HtmlDecode(playlist.Description ?? "").Trim();
            changes.Add((playlist, current, entry.Description));
        }

        var toWrite = changes.Where(c => c.current != c.wanted).ToList();

        foreach (var (playlist, current, wanted) in toWrite)
        {
            Console.WriteLine($"'{playlist.Name}'");
            Console.WriteLine($"  was: {(current.Length == 0 ? "(none)" : current)}");
            Console.WriteLine($"  now: {wanted}");
        }

        int unchanged = changes.Count - toWrite.Count;
        Console.WriteLine();
        Console.WriteLine($"{toWrite.Count} description(s) to set, {unchanged} already as given.");

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        foreach (var (playlist, _, wanted) in toWrite)
        {
            await spotify.Playlists.ChangeDetails(playlist.Id, new PlaylistChangeDetailsRequest { Description = wanted });
        }

        ConsoleWrapper.WriteSuccess($"Set {toWrite.Count} description(s).");
        return 0;
    }

    #endregion

}
