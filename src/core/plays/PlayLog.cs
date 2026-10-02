namespace core.plays;

/// <summary>What one poll of the recently-played list added to the play log.</summary>
public class PlayLogResult
{

    /// <summary>How many plays Spotify returned.</summary>
    public int Fetched { get; init; }

    /// <summary>How many of them were not on record yet.</summary>
    public int Added { get; init; }

    /// <summary>
    /// True when plays were probably lost between this poll and the one before: Spotify only
    /// shows the last fifty, and none of them was already on record.
    /// </summary>
    public bool MayHaveMissedPlays { get; init; }

    public bool DryRun { get; init; }

}

/// <summary>The decisions of the play log that need neither Spotify nor the database.</summary>
public static class PlayLog
{

    #region Public Methods

    /// <summary>
    /// The fetched plays that are not on record yet. Plays arrive in the order they happened,
    /// so anything later than the last recorded play is new.
    /// </summary>
    public static IReadOnlyList<PlayRecord> SelectNew(DateTime? latestRecorded, IEnumerable<PlayRecord> fetched)
    {
        return fetched
            .Where(p => latestRecorded == null || p.PlayedAt > latestRecorded)
            .OrderBy(p => p.PlayedAt)
            .ToList();
    }

    /// <summary>
    /// Whether plays may have dropped off Spotify's list unrecorded: there is a log to continue,
    /// the page came back full, and every play on it is new. With no overlap there is no telling
    /// how many came in between.
    /// </summary>
    public static bool MayHaveMissedPlays(DateTime? latestRecorded, IReadOnlyCollection<PlayRecord> fetched, int pageSize)
    {
        return latestRecorded != null
               && fetched.Count >= pageSize
               && fetched.All(p => p.PlayedAt > latestRecorded);
    }

    #endregion

}
