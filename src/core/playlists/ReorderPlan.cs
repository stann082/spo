namespace core.playlists;

/// <summary>One item of a playlist being reordered. Episodes have no artists.</summary>
public record PlaylistEntry(string Uri, string Display, IReadOnlyList<string> Artists);

/// <summary>
/// One single-track move, in the terms Spotify's reorder call uses: both positions count from 0
/// in the playlist as it is just before this move.
/// </summary>
/// <param name="From">Where the track is.</param>
/// <param name="InsertBefore">The position it is put in front of; the playlist length puts it last.</param>
/// <param name="Item">Which track it is: its position before any move.</param>
public record ReorderMove(int From, int InsertBefore, int Item);

/// <summary>
/// The fewest single-track moves that turn a playlist into a wanted order. Moving tracks, rather
/// than replacing the playlist's contents, keeps each track's date added and cannot lose a track
/// when a request fails half-way.
/// </summary>
public class ReorderPlan
{

    #region Properties

    /// <summary>The wanted order: for each new position, the track's current position.</summary>
    public IReadOnlyList<int> Order { get; private init; } = [];

    /// <summary>The moves, to be applied one after another.</summary>
    public IReadOnlyList<ReorderMove> Moves { get; private init; } = [];

    #endregion

    #region Public Methods

    /// <summary>
    /// Works out which current position each track of the file is at. The file must list exactly
    /// what the playlist holds: a track that is in the playlist twice is listed twice.
    /// </summary>
    public static IReadOnlyList<int> ResolveOrder(ReorderDefinition definition, string playlistName, IReadOnlyList<PlaylistEntry> items)
    {
        var positions = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            if (!positions.TryGetValue(items[i].Uri, out var queue))
            {
                positions[items[i].Uri] = queue = new Queue<int>();
            }

            queue.Enqueue(i);
        }

        var order = new List<int>();
        var notInPlaylist = new List<string>();
        foreach (var track in definition.Tracks)
        {
            if (positions.TryGetValue(track.Uri, out var queue) && queue.Count > 0)
            {
                order.Add(queue.Dequeue());
            }
            else
            {
                notInPlaylist.Add(string.IsNullOrWhiteSpace(track.Title) ? track.Id : $"{track} ({track.Id})");
            }
        }

        var errors = new List<string>();

        if (notInPlaylist.Count > 0)
        {
            errors.Add($"Track(s) not in '{playlistName}', or listed more often than they are in it: {string.Join("; ", notInPlaylist)}");
        }

        var unlisted = positions.Values.SelectMany(q => q).OrderBy(i => i).Select(i => items[i].Display).ToList();
        if (unlisted.Count > 0)
        {
            errors.Add($"The file must list every track of '{playlistName}'; missing: {string.Join("; ", unlisted)}");
        }

        if (errors.Count > 0)
        {
            throw new SpoException($"The reorder cannot run:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        return order;
    }

    /// <param name="order">For each new position, the track's current position; every position exactly once.</param>
    public static ReorderPlan Build(IReadOnlyList<int> order)
    {
        if (order.Count != order.Distinct().Count() || order.Any(i => i < 0 || i >= order.Count))
        {
            throw new ArgumentException("The order must hold every current position exactly once.", nameof(order));
        }

        // Tracks already in the right order relative to each other stay put; only the rest move.
        var staying = LongestIncreasingRun(order);

        var current = Enumerable.Range(0, order.Count).ToList();
        var moves = new List<ReorderMove>();

        for (int k = 0; k < order.Count; k++)
        {
            int item = order[k];
            if (staying.Contains(item))
            {
                continue;
            }

            // Everything before it in the wanted order is already in place, so it goes right
            // behind its predecessor.
            int from = current.IndexOf(item);
            int insertBefore = k == 0 ? 0 : current.IndexOf(order[k - 1]) + 1;
            if (from == insertBefore)
            {
                continue;
            }

            moves.Add(new ReorderMove(from, insertBefore, item));
            Apply(current, from, insertBefore);
        }

        return new ReorderPlan { Order = order.ToList(), Moves = moves };
    }

    /// <summary>What a playlist looks like after <paramref name="moves"/>, the way Spotify applies them.</summary>
    public static List<T> Apply<T>(IEnumerable<T> items, IEnumerable<ReorderMove> moves)
    {
        var list = items.ToList();
        foreach (var move in moves)
        {
            Apply(list, move.From, move.InsertBefore);
        }

        return list;
    }

    #endregion

    #region Helper Methods

    private static void Apply<T>(List<T> list, int from, int insertBefore)
    {
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(insertBefore > from ? insertBefore - 1 : insertBefore, item);
    }

    /// <summary>The values of one longest increasing subsequence, by patience sorting.</summary>
    private static HashSet<int> LongestIncreasingRun(IReadOnlyList<int> values)
    {
        // tails[len] is the index of the smallest value that ends an increasing run of len + 1.
        var tails = new List<int>();
        var previous = new int[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            int low = 0;
            int high = tails.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (values[tails[middle]] < values[i])
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[i] = low > 0 ? tails[low - 1] : -1;
            if (low == tails.Count)
            {
                tails.Add(i);
            }
            else
            {
                tails[low] = i;
            }
        }

        var run = new HashSet<int>();
        for (int i = tails.Count > 0 ? tails[^1] : -1; i >= 0; i = previous[i])
        {
            run.Add(values[i]);
        }

        return run;
    }

    #endregion

}
