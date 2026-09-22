namespace core.monitor;

/// <summary>
/// One capture of a single top list (artists or tracks, for one time range).
/// </summary>
public class TopSnapshot
{

    #region Properties

    /// <summary>Store-assigned row id. Zero until the snapshot has been saved.</summary>
    public long Id { get; set; }

    public DateTime CapturedAt { get; set; }

    public TopEntityType EntityType { get; set; }

    /// <summary>Spotify time range: "short", "medium" or "long".</summary>
    public string TimeRange { get; set; }

    public IReadOnlyList<TopEntry> Entries { get; set; } = [];

    #endregion

}
