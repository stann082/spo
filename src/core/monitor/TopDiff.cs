namespace core.monitor;

/// <summary>
/// The change between two snapshots of the same list.
/// </summary>
public class TopDiff
{

    #region Properties

    public TopEntityType EntityType { get; init; }

    public string TimeRange { get; init; }

    /// <summary>Null on the very first run, when there is nothing to compare against.</summary>
    public DateTime? PreviousCapturedAt { get; init; }

    public DateTime CurrentCapturedAt { get; init; }

    /// <summary>Number of entries in the current snapshot - the "top N" the report talks about.</summary>
    public int ListSize { get; init; }

    /// <summary>
    /// Entries that entered, then left, then moved (largest move first). Entries that held their
    /// rank are excluded.
    /// </summary>
    public IReadOnlyList<EntryChange> Changes { get; init; } = [];

    /// <summary>True when there was no previous snapshot, so this run only established a baseline.</summary>
    public bool IsBaseline => PreviousCapturedAt == null;

    public bool HasChanges => Changes.Count > 0;

    public string Label => EntityType == TopEntityType.Artist ? "artists" : "tracks";

    public IEnumerable<EntryChange> Entered => Changes.Where(c => c.Kind == ChangeKind.Entered);

    public IEnumerable<EntryChange> Left => Changes.Where(c => c.Kind == ChangeKind.Left);

    public IEnumerable<EntryChange> Moved =>
        Changes.Where(c => c.Kind is ChangeKind.MovedUp or ChangeKind.MovedDown);

    #endregion

    #region Public Methods

    /// <summary>
    /// A short headline such as "Top artists: 2 new, 1 out, 6 moved".
    /// </summary>
    public string Summarize()
    {
        if (IsBaseline)
        {
            return $"Top {Label}: baseline of {ListSize} recorded";
        }

        if (!HasChanges)
        {
            return $"Top {Label}: no change";
        }

        var parts = new List<string>();
        int entered = Entered.Count();
        int left = Left.Count();
        int moved = Moved.Count();

        if (entered > 0) parts.Add($"{entered} new");
        if (left > 0) parts.Add($"{left} out");
        if (moved > 0) parts.Add($"{moved} moved");

        return $"Top {Label}: {string.Join(", ", parts)}";
    }

    #endregion

}
