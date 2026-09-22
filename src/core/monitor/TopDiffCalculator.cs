namespace core.monitor;

/// <summary>
/// Compares two snapshots of the same list. Entries are matched on their Spotify id, so a
/// rename or a re-release does not read as one entry leaving and another entering.
/// </summary>
public static class TopDiffCalculator
{

    #region Public Methods

    /// <summary>
    /// Builds the diff from <paramref name="previous"/> to <paramref name="current"/>. Pass a null
    /// previous snapshot for the first ever run; the result is then a baseline with no changes.
    /// </summary>
    public static TopDiff Compare(TopSnapshot previous, TopSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous == null)
        {
            return new TopDiff
            {
                EntityType = current.EntityType,
                TimeRange = current.TimeRange,
                PreviousCapturedAt = null,
                CurrentCapturedAt = current.CapturedAt,
                ListSize = current.Entries.Count,
                Changes = []
            };
        }

        var previousById = Index(previous.Entries);
        var currentById = Index(current.Entries);

        var entered = new List<EntryChange>();
        var moved = new List<EntryChange>();

        foreach (var entry in current.Entries.OrderBy(e => e.Rank))
        {
            if (!previousById.TryGetValue(entry.SpotifyId, out var before))
            {
                entered.Add(new EntryChange
                {
                    Kind = ChangeKind.Entered,
                    SpotifyId = entry.SpotifyId,
                    Name = entry.Name,
                    Detail = entry.Detail,
                    PreviousRank = null,
                    CurrentRank = entry.Rank
                });
                continue;
            }

            if (before.Rank == entry.Rank)
            {
                continue;
            }

            moved.Add(new EntryChange
            {
                // A smaller rank number is a better position, so a drop in rank is a move up.
                Kind = entry.Rank < before.Rank ? ChangeKind.MovedUp : ChangeKind.MovedDown,
                SpotifyId = entry.SpotifyId,
                Name = entry.Name,
                Detail = entry.Detail,
                PreviousRank = before.Rank,
                CurrentRank = entry.Rank
            });
        }

        var left = previous.Entries
            .Where(e => !currentById.ContainsKey(e.SpotifyId))
            .OrderBy(e => e.Rank)
            .Select(e => new EntryChange
            {
                Kind = ChangeKind.Left,
                SpotifyId = e.SpotifyId,
                Name = e.Name,
                Detail = e.Detail,
                PreviousRank = e.Rank,
                CurrentRank = null
            })
            .ToList();

        var changes = new List<EntryChange>(entered.Count + left.Count + moved.Count);
        changes.AddRange(entered);
        changes.AddRange(left);
        changes.AddRange(moved.OrderByDescending(c => Math.Abs(c.Delta)).ThenBy(c => c.CurrentRank));

        return new TopDiff
        {
            EntityType = current.EntityType,
            TimeRange = current.TimeRange,
            PreviousCapturedAt = previous.CapturedAt,
            CurrentCapturedAt = current.CapturedAt,
            ListSize = current.Entries.Count,
            Changes = changes
        };
    }

    #endregion

    #region Helper Methods

    private static Dictionary<string, TopEntry> Index(IReadOnlyList<TopEntry> entries)
    {
        var index = new Dictionary<string, TopEntry>(entries.Count, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            // Spotify should not repeat an id within one list, but a duplicate must not crash the run.
            index.TryAdd(entry.SpotifyId, entry);
        }

        return index;
    }

    #endregion

}
