namespace core.monitor;

public interface ISnapshotStore
{

    /// <summary>Creates the database and schema if they are not there yet. Safe to call on every run.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>The most recently captured snapshot for a list, or null if none has been recorded.</summary>
    Task<TopSnapshot> GetLatestAsync(TopEntityType entityType, string timeRange, CancellationToken cancellationToken = default);

    /// <summary>Persists a snapshot and assigns its <see cref="TopSnapshot.Id"/>.</summary>
    Task SaveAsync(TopSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Deletes snapshots older than the cutoff. Returns how many were removed.</summary>
    Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);

}
