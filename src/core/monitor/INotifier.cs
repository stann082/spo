namespace core.monitor;

public interface INotifier
{

    /// <summary>
    /// Surfaces the outcome of a monitor run to the user. Implementations are expected to swallow
    /// their own delivery failures rather than take the run down with them.
    /// </summary>
    Task NotifyAsync(MonitorRunResult result, CancellationToken cancellationToken = default);

}
