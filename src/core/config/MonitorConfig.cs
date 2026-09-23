namespace core.config;

public class MonitorConfig
{

    /// <summary>How many artists and tracks to track per list. Spotify caps this at 50.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>Which Spotify time ranges to track: "short" (4 weeks), "medium" (6 months), "long" (all time).</summary>
    public string[] TimeRanges { get; set; } = ["short"];

    /// <summary>Days of history to keep. Zero or less keeps everything.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>
    /// Name of the playlist kept equal to your top 50 tracks of the last 4 weeks, in rank order.
    /// "{year}" becomes the current year, so each year gets its own playlist. Empty turns it off.
    /// </summary>
    public string FavoritesPlaylist { get; set; } = "{year} Favs";

}
