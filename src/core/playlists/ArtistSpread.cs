namespace core.playlists;

/// <summary>
/// Pulls apart tracks that share an artist and sit back to back, featured artists included,
/// while leaving everything else where it is. Each track that has to move goes to the nearest
/// slot where it sits next to neither of its artists.
/// </summary>
public static class ArtistSpread
{

    #region Public Methods

    /// <returns>
    /// The new order, as indices into <paramref name="items"/>. Pairs that cannot be separated
    /// (a playlist that is mostly one artist) are left as they are.
    /// </returns>
    public static IReadOnlyList<int> Arrange(IReadOnlyList<PlaylistEntry> items)
    {
        var artists = ArtistSets(items);
        var order = Enumerable.Range(0, items.Count).ToList();
        var stuck = new HashSet<(int, int)>();

        while (true)
        {
            int at = FirstClash(order, artists, stuck);
            if (at < 0)
            {
                return order;
            }

            var pair = (order[at - 1], order[at]);

            // Every move takes away more clashes than it leaves behind, so this ends.
            if (!TryMove(order, at, artists) && !TryMove(order, at - 1, artists))
            {
                stuck.Add(pair);
            }
        }
    }

    /// <summary>How many neighbouring pairs in <paramref name="order"/> share an artist.</summary>
    public static int CountClashes(IReadOnlyList<PlaylistEntry> items, IReadOnlyList<int> order)
    {
        var artists = ArtistSets(items);
        int clashes = 0;
        for (int i = 1; i < order.Count; i++)
        {
            if (artists[order[i - 1]].Overlaps(artists[order[i]]))
            {
                clashes++;
            }
        }

        return clashes;
    }

    #endregion

    #region Helper Methods

    private static List<HashSet<string>> ArtistSets(IReadOnlyList<PlaylistEntry> items)
    {
        return items.Select(i => new HashSet<string>(i.Artists ?? [], StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static int FirstClash(List<int> order, List<HashSet<string>> artists, HashSet<(int, int)> stuck)
    {
        for (int i = 1; i < order.Count; i++)
        {
            if (artists[order[i - 1]].Overlaps(artists[order[i]]) && !stuck.Contains((order[i - 1], order[i])))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Moves the track at <paramref name="at"/> to the nearest clash-free slot, but only when
    /// that leaves fewer clashes than before: taking it out must not just make its two old
    /// neighbours clash instead.
    /// </summary>
    private static bool TryMove(List<int> order, int at, List<HashSet<string>> artists)
    {
        int item = order[at];
        bool hasLeft = at > 0;
        bool hasRight = at < order.Count - 1;

        int solved = (hasLeft && artists[order[at - 1]].Overlaps(artists[item]) ? 1 : 0)
                     + (hasRight && artists[item].Overlaps(artists[order[at + 1]]) ? 1 : 0);
        int created = hasLeft && hasRight && artists[order[at - 1]].Overlaps(artists[order[at + 1]]) ? 1 : 0;
        if (created >= solved)
        {
            return false;
        }

        order.RemoveAt(at);

        for (int distance = 1; distance <= order.Count; distance++)
        {
            // Later slots first: a track that drifts down the list reads as less of a change.
            foreach (int slot in new[] { at + distance, at - distance })
            {
                if (slot < 0 || slot > order.Count)
                {
                    continue;
                }

                bool clearBefore = slot == 0 || !artists[order[slot - 1]].Overlaps(artists[item]);
                bool clearAfter = slot == order.Count || !artists[order[slot]].Overlaps(artists[item]);
                if (clearBefore && clearAfter)
                {
                    order.Insert(slot, item);
                    return true;
                }
            }
        }

        order.Insert(at, item);
        return false;
    }

    #endregion

}
