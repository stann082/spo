namespace core.plays;

public interface IPlayStore
{

    /// <summary>Creates the database and schema if they are not there yet. Safe to call on every run.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>When the most recent play on record ended, or null if nothing has been recorded.</summary>
    Task<DateTime?> GetLatestPlayedAtAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds plays that are not on record yet. Returns how many were new.</summary>
    Task<int> SaveAsync(IReadOnlyList<PlayRecord> plays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks by number of plays, most played first.
    /// </summary>
    /// <param name="query">Only tracks whose name or artists contain this text; null for all.</param>
    /// <param name="sinceUtc">Only plays from this moment on; null for all.</param>
    Task<IReadOnlyList<PlayCount>> GetCountsAsync(int limit, string query = null, DateTime? sinceUtc = null, CancellationToken cancellationToken = default);

    Task<PlayLogSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

}
