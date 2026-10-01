using System.Net;
using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Sets the descriptions of existing playlists, and optionally renames them, from a file. Every
/// playlist is looked up and every new name checked before anything changes, and values that
/// already match are left alone.
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
        var changes = new List<Change>();
        foreach (var entry in definition.Playlists)
        {
            var playlist = PlaylistLookup.Find(playlists, entry.Name);
            PlaylistLookup.RequireOwned(playlist, me.Id, "changed");

            // Spotify hands descriptions back HTML-escaped.
            var currentDescription = WebUtility.HtmlDecode(playlist.Description ?? "").Trim();
            var newDescription = entry.Description != null && entry.Description != currentDescription ? entry.Description : null;
            var newName = entry.Rename != null && entry.Rename != playlist.Name ? entry.Rename : null;

            changes.Add(new Change(playlist, currentDescription, newDescription, newName));
        }

        var conflicts = DescribeDefinition.FindNameConflicts(
            changes.Where(c => c.NewName != null).Select(c => (c.Playlist.Name, c.NewName)).ToList(),
            playlists.Select(p => p.Name));
        if (conflicts.Count > 0)
        {
            throw new SpoException(string.Join(Environment.NewLine, conflicts));
        }

        var toWrite = changes.Where(c => c.NewDescription != null || c.NewName != null).ToList();
        foreach (var change in toWrite)
        {
            Console.WriteLine($"'{change.Playlist.Name}'");
            if (change.NewName != null)
            {
                Console.WriteLine($"  rename: '{change.Playlist.Name}' -> '{change.NewName}'");
            }

            if (change.NewDescription != null)
            {
                Console.WriteLine($"  was: {(change.CurrentDescription.Length == 0 ? "(none)" : change.CurrentDescription)}");
                Console.WriteLine($"  now: {change.NewDescription}");
            }
        }

        int descriptions = toWrite.Count(c => c.NewDescription != null);
        int renames = toWrite.Count(c => c.NewName != null);
        Console.WriteLine();
        Console.WriteLine($"{descriptions} description(s) to set, {renames} rename(s), {changes.Count - toWrite.Count} playlist(s) already as given.");

        if (dryRun)
        {
            ConsoleWrapper.WriteInfo("Dry run: nothing was changed.");
            return 0;
        }

        foreach (var change in toWrite)
        {
            var request = new PlaylistChangeDetailsRequest();
            if (change.NewName != null)
            {
                request.Name = change.NewName;
            }

            if (change.NewDescription != null)
            {
                request.Description = change.NewDescription;
            }

            await spotify.Playlists.ChangeDetails(change.Playlist.Id, request);
        }

        ConsoleWrapper.WriteSuccess($"Set {descriptions} description(s) and {renames} rename(s).");
        return 0;
    }

    #endregion

    #region Helper Types

    /// <param name="NewDescription">Null when the description stays as it is.</param>
    /// <param name="NewName">Null when the name stays as it is.</param>
    private record Change(SimplePlaylist Playlist, string CurrentDescription, string NewDescription, string NewName);

    #endregion

}
