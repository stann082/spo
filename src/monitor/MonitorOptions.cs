using CommandLine;

namespace monitor;

public class MonitorOptions
{

    [Option("dry-run", HelpText = "Fetch and report the diff without writing anything to the history database.")]
    public bool DryRun { get; set; }

    [Option("no-notify", HelpText = "Skip the toast notification; write the report to the console and log only.")]
    public bool NoNotify { get; set; }

    [Option("notify-always", HelpText = "Show a toast even when nothing changed. By default a no-change run stays quiet.")]
    public bool NotifyAlways { get; set; }

}
