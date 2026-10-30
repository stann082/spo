using System.Globalization;
using cli.options;
using core;
using core.export;
using core.plays;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Play counts from the local play log. The monitor fills the log and this only reads it; Spotify
/// is asked only for a playlist's tracks, when the counts are limited to one.
/// </summary>
public static class PlaysCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(PlaysOptions options, IPlayStore store, ISpotifyClientFactory clientFactory)
    {
        if (options.Limit < 1)
        {
            throw new SpoException("--limit must be at least 1.");
        }

        bool json = OutputFormat.Parse(options.Format, "text", "json") == "json";
        var since = ParseSince(options.Since);
        var playlist = string.IsNullOrWhiteSpace(options.Playlist) ? null : await GetPlaylistAsync(clientFactory, options.Playlist);

        await store.InitializeAsync();

        // With a playlist, every played track is fetched so the summary can say how much of it was played.
        var matching = await store.GetCountsAsync(playlist == null ? options.Limit : int.MaxValue, options.Query, since, playlist?.TrackIds);
        var counts = matching.Take(options.Limit).ToList();

        if (json)
        {
            // Always an array, even an empty one, so scripts never have to special-case "no plays".
            Console.WriteLine(JsonOutput.Serialize(counts));
            return 0;
        }

        var summary = await store.GetSummaryAsync();
        if (summary.Plays == 0)
        {
            Console.WriteLine("No plays logged yet. The monitor records them: run scripts\\install-monitor.ps1, or spo-monitor --plays-only.");
            return 0;
        }

        Console.WriteLine($"{summary.Plays} play(s) of {summary.Tracks} track(s) logged since {summary.FirstPlayed.Value.ToLocalTime():yyyy-MM-dd}.");
        if (playlist != null)
        {
            Console.WriteLine($"'{playlist.Name}': {matching.Count} of its {playlist.TrackIds.Count} track(s) played, {matching.Sum(c => c.Plays)} play(s).");
        }

        Console.WriteLine();

        if (counts.Count == 0)
        {
            Console.WriteLine("No plays match.");
            return 0;
        }

        int width = counts.Max(c => c.Plays).ToString().Length;
        foreach (var count in counts)
        {
            Console.WriteLine($"  {count.Plays.ToString().PadLeft(width)}  {count.Name} - {count.Artists}  (last {count.LastPlayed.ToLocalTime():yyyy-MM-dd})");
        }

        return 0;
    }

    #endregion

    #region Helper Methods

    private record PlaylistTracks(string Name, IReadOnlyCollection<string> TrackIds);

    /// <summary>The ids of a playlist's tracks; local files and podcast episodes have none and are left out.</summary>
    private static async Task<PlaylistTracks> GetPlaylistAsync(ISpotifyClientFactory clientFactory, string nameOrId)
    {
        var spotify = clientFactory.CreateUserClient();
        var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });
        var playlist = PlaylistLookup.Find(await spotify.PaginateAll(page), nameOrId);

        var itemsPage = await spotify.Playlists.GetItems(playlist.Id);
        var ids = (await spotify.PaginateAll(itemsPage))
            .Select(item => item.Track as FullTrack)
            .Where(track => !string.IsNullOrEmpty(track?.Id))
            .Select(track => track.Id)
            .ToHashSet();

        return new PlaylistTracks(playlist.Name, ids);
    }

    /// <summary>The start of that day here, as UTC; null when no date was given.</summary>
    private static DateTime? ParseSince(string since)
    {
        if (string.IsNullOrWhiteSpace(since))
        {
            return null;
        }

        if (!DateTime.TryParseExact(since.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var day))
        {
            throw new SpoException($"--since must be a date as yyyy-MM-dd, not '{since}'.");
        }

        return day.ToUniversalTime();
    }

    #endregion

}
