using SpotifyAPI.Web;

namespace core.spotify;

public static class SpotifyTimeRange
{

    public static readonly IReadOnlyList<string> Names = ["short", "medium", "long"];

    /// <summary>
    /// Maps the CLI/config spelling of a time range onto Spotify's enum. An unknown value is an
    /// error rather than a silent fallback, so a typo in config.json cannot quietly track the
    /// wrong list.
    /// </summary>
    public static PersonalizationTopRequest.TimeRange Parse(string range)
    {
        return range switch
        {
            "short" => PersonalizationTopRequest.TimeRange.ShortTerm,
            "medium" => PersonalizationTopRequest.TimeRange.MediumTerm,
            "long" => PersonalizationTopRequest.TimeRange.LongTerm,
            _ => throw new SpoException($"Unknown time range '{range}'. Use one of: {string.Join(", ", Names)}.")
        };
    }

    /// <summary>A human-readable window, e.g. "the last 4 weeks".</summary>
    public static string Describe(string range)
    {
        return range switch
        {
            "short" => "the last 4 weeks",
            // Spotify documents long_term as about a year of data, not the whole history.
            "long" => "the last 12 months",
            _ => "the last 6 months"
        };
    }

}
