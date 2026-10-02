using CommandLine;

namespace cli.options;

[Verb("plays", HelpText = "Show how often you played each track, from the play log the monitor keeps. Spotify has no play counts of its own, so counting starts when the monitor's half-hourly run is installed.")]
public class PlaysOptions
{

    [Option('l', "limit", Default = 20, HelpText = "How many tracks to show, most played first.")]
    public int Limit { get; set; }

    [Option('q', "query", HelpText = "Only tracks whose title or artists contain this text (case-insensitive).")]
    public string Query { get; set; }

    [Option("since", MetaValue = "DATE", HelpText = "Only count plays from this day on, as yyyy-MM-dd.")]
    public string Since { get; set; }

    [Option('f', "format", Default = "text", HelpText = "Output format: text or json.")]
    public string Format { get; set; }

}
