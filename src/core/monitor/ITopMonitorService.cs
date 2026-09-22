namespace core.monitor;

public interface ITopMonitorService
{

    /// <summary>
    /// Fetches the current top artists and tracks, compares them against the last stored
    /// snapshots and records the new ones.
    /// </summary>
    /// <param name="persist">
    /// When false the results are still fetched and diffed but nothing is written, which is what
    /// a dry run uses to preview a report without disturbing the history.
    /// </param>
    Task<MonitorRunResult> RunAsync(bool persist = true, CancellationToken cancellationToken = default);

}
