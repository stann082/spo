using cli.options;
using core;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

public static class TopCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(TopOptions options, ISpotifyClientFactory clientFactory)
    {
        if (options.Limit is < 1 or > SpotifyLimits.TopItemsMax)
        {
            throw new SpoException($"--limit must be between 1 and {SpotifyLimits.TopItemsMax}.");
        }

        var request = new PersonalizationTopRequest
        {
            Limit = options.Limit,
            TimeRangeParam = SpotifyTimeRange.Parse(options.Range)
        };

        var spotify = clientFactory.CreateUserClient();
        bool showBoth = !options.Artists && !options.Tracks;

        if (options.Artists || showBoth)
        {
            var artists = await spotify.Personalization.GetTopArtists(request);
            PrintList("Top Artists", artists.Items, a =>
                a.Genres.Count > 0 ? $"{a.Name} [{string.Join(", ", a.Genres.Take(2))}]" : a.Name);
        }

        if (options.Tracks || showBoth)
        {
            var tracks = await spotify.Personalization.GetTopTracks(request);
            PrintList("Top Tracks", tracks.Items, t =>
                $"{t.Name} - {string.Join(", ", t.Artists.Select(a => a.Name))}");
        }

        return 0;
    }

    #endregion

    #region Helper Methods

    private static void PrintList<T>(string heading, IEnumerable<T> items, Func<T, string> describe)
    {
        Console.WriteLine($"{heading}:");
        int rank = 1;
        foreach (var item in items ?? [])
        {
            Console.WriteLine($"  {rank++,2}. {describe(item)}");
        }

        Console.WriteLine();
    }

    #endregion

}
