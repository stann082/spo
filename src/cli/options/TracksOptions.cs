using CommandLine;
using core.spotify;

namespace cli.options;

[Verb("tracks", HelpText = "Show your recently played tracks.")]
public class TracksOptions
{

    [Option('r', "recent", Default = SpotifyLimits.RecentlyPlayedMax, HelpText = "Number of recently played tracks (1-50).")]
    public int Recent { get; set; }

    [Option('t', "time", HelpText = "Show when each track was played (inline format; the table always shows it).")]
    public bool DisplayTime { get; set; }

    [Option('f', "format", Default = "inline", HelpText = "Output format: inline or table.")]
    public string Format { get; set; }

}
