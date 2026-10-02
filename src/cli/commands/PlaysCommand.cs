using System.Globalization;
using cli.options;
using core;
using core.export;
using core.plays;

namespace cli.commands;

/// <summary>
/// Play counts from the local play log. Nothing here talks to Spotify: the monitor fills the log,
/// this only reads it.
/// </summary>
public static class PlaysCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(PlaysOptions options, IPlayStore store)
    {
        if (options.Limit < 1)
        {
            throw new SpoException("--limit must be at least 1.");
        }

        bool json = OutputFormat.Parse(options.Format, "text", "json") == "json";
        var since = ParseSince(options.Since);

        await store.InitializeAsync();
        var counts = await store.GetCountsAsync(options.Limit, options.Query, since);

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
