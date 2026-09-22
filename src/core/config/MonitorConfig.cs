namespace core.config;

public class MonitorConfig
{

    /// <summary>How many artists and tracks to track per list. Spotify caps this at 50.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>Which Spotify time ranges to track: "short" (4 weeks), "medium" (6 months), "long" (all time).</summary>
    public string[] TimeRanges { get; set; } = ["short"];

    /// <summary>Days of history to keep. Zero or less keeps everything.</summary>
    public int RetentionDays { get; set; } = 365;

}
