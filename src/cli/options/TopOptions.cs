using CommandLine;

namespace cli.options;

[Verb("top", HelpText = "Show your top artists and tracks.")]
public class TopOptions
{

    [Option("artists", HelpText = "Only show top artists.")]
    public bool Artists { get; set; }

    [Option("tracks", HelpText = "Only show top tracks.")]
    public bool Tracks { get; set; }

    [Option('l', "limit", Default = 10, HelpText = "Number of results per list (1-50).")]
    public int Limit { get; set; }

    [Option('r', "range", Default = "medium", HelpText = "Time range: short (4 weeks), medium (6 months), long (12 months).")]
    public string Range { get; set; }

}
