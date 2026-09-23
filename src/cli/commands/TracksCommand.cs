using cli.options;
using core;
using core.export;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

public static class TracksCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(TracksOptions options, ISpotifyClientFactory clientFactory)
    {
        if (options.Recent is < 1 or > SpotifyLimits.RecentlyPlayedMax)
        {
            throw new SpoException($"--recent must be between 1 and {SpotifyLimits.RecentlyPlayedMax}.");
        }

        var format = OutputFormat.Parse(options.Format, "inline", "table", "json");

        var spotify = clientFactory.CreateUserClient();
        var paging = await spotify.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest { Limit = options.Recent });

        var history = (paging.Items ?? [])
            .Where(i => i.Track != null)
            .OrderByDescending(i => i.PlayedAt)
            .ToList();

        if (format == "json")
        {
            // Always an array, even an empty one, so scripts never have to special-case "no plays".
            Console.WriteLine(JsonOutput.Serialize(history.Select(i => TrackRecord.From(i.Track, playedAt: i.PlayedAt))));
            return 0;
        }

        var plays = history
            .Select(i => new Play(
                i.Track.Name,
                string.Join(", ", i.Track.Artists.Select(a => a.Name)),
                i.PlayedAt.ToLocalTime().ToString()))
            .ToList();

        if (plays.Count == 0)
        {
            Console.WriteLine("No recently played tracks.");
            return 0;
        }

        if (format == "table")
        {
            PrintTable(plays);
        }
        else
        {
            PrintInline(plays, options.DisplayTime);
        }

        return 0;
    }

    #endregion

    #region Helper Methods

    private static void PrintInline(IEnumerable<Play> plays, bool displayTime)
    {
        foreach (var play in plays)
        {
            Console.WriteLine(displayTime
                ? $"{play.Song} - {play.Artists} [Played at {play.PlayedAt}]"
                : $"{play.Song} - {play.Artists}");
        }
    }

    private static void PrintTable(IReadOnlyList<Play> plays)
    {
        var header = new Play("Song", "Artist", "Played at");
        int songWidth = plays.Append(header).Max(p => p.Song.Length) + 3;
        int artistWidth = plays.Append(header).Max(p => p.Artists.Length) + 3;

        foreach (var play in plays.Prepend(header))
        {
            Console.WriteLine($"{play.Song.PadRight(songWidth)} {play.Artists.PadRight(artistWidth)} {play.PlayedAt}");
        }
    }

    #endregion

    #region Helper Classes

    private record Play(string Song, string Artists, string PlayedAt);

    #endregion

}
