namespace core.monitor;

/// <summary>
/// Everything one pass of the monitor produced: one diff per tracked list.
/// </summary>
public class MonitorRunResult
{

    public DateTime CapturedAt { get; init; }

    public IReadOnlyList<TopDiff> Diffs { get; init; } = [];

    /// <summary>True when every list was a first capture, so there was nothing to compare against.</summary>
    public bool IsBaseline => Diffs.Count > 0 && Diffs.All(d => d.IsBaseline);

    /// <summary>
    /// True when some lists were captured for the first time alongside lists with history - for
    /// example after adding a time range to the monitor config.
    /// </summary>
    public bool HasNewLists => !IsBaseline && Diffs.Any(d => d.IsBaseline);

    public bool HasChanges => Diffs.Any(d => d.HasChanges);

    /// <summary>
    /// Whether this run is worth a notification. A run where nothing moved stays quiet, but
    /// starting to track a list is always surfaced so it cannot happen unnoticed.
    /// </summary>
    public bool IsWorthNotifying => IsBaseline || HasNewLists || HasChanges;

    /// <summary>Snapshots discarded by retention on this run.</summary>
    public int PrunedSnapshots { get; init; }

    /// <summary>What happened to the favorites playlist; null when it is turned off.</summary>
    public FavoritesSyncResult Favorites { get; init; }

}
