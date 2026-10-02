using CommandLine;

namespace monitor;

public class MonitorOptions
{

    [Option("dry-run", HelpText = "Fetch and report without writing anything to the history database or to Spotify.")]
    public bool DryRun { get; set; }

    [Option("plays-only", HelpText = "Only copy your recently played tracks into the play log: no top lists, no playlists, no toast. Meant to run every half hour, since Spotify shows just the last 50 plays.")]
    public bool PlaysOnly { get; set; }

    [Option("no-notify",HelpText = "Skip the toast notification; write the report to the console and log only.")]
    public bool NoNotify { get; set; }

    [Option("notify-always", HelpText = "Show a toast even when nothing changed. By default a no-change run stays quiet.")]
    public bool NotifyAlways { get; set; }

}
