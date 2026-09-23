namespace core.monitor;

/// <summary>What keeping the favorites playlist in line with the top tracks takes.</summary>
/// <param name="Added">Tracks in the top list that the playlist does not have, in rank order.</param>
/// <param name="Removed">Tracks in the playlist that dropped out of the top list, in playlist order.</param>
/// <param name="Reordered">True when the tracks that stay are in a different order than their ranks.</param>
public record FavoritesPlan(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, bool Reordered)
{

    public bool IsUnchanged => Added.Count == 0 && Removed.Count == 0 && !Reordered;

}

/// <summary>
/// The pure part of the favorites playlist: its name for a given year, and what has to change to
/// make it match the current top tracks.
/// </summary>
public static class FavoritesPlaylist
{

    #region Constants

    public const string YearPlaceholder = "{year}";

    #endregion

    #region Public Methods

    /// <summary>"{year} Favs" on 2026-09-23 is "2026 Favs". A new year gets a new playlist, which leaves last year's as its final snapshot.</summary>
    public static string Name(string format, DateTime now)
    {
        return format.Replace(YearPlaceholder, now.Year.ToString(), StringComparison.OrdinalIgnoreCase).Trim();
    }

    /// <param name="current">Track uris in the playlist now, in playlist order.</param>
    /// <param name="desired">Track uris of the top list, in rank order.</param>
    public static FavoritesPlan Plan(IReadOnlyList<string> current, IReadOnlyList<string> desired)
    {
        var desiredSet = new HashSet<string>(desired, StringComparer.Ordinal);
        var currentSet = new HashSet<string>(current, StringComparer.Ordinal);

        var added = desired.Where(uri => !currentSet.Contains(uri)).Distinct(StringComparer.Ordinal).ToList();
        var removed = current.Where(uri => !desiredSet.Contains(uri)).Distinct(StringComparer.Ordinal).ToList();

        // Compare only the tracks that stay: an add or a removal alone does not count as a reorder.
        var keptInPlaylist = current.Where(desiredSet.Contains);
        var keptInRanks = desired.Where(currentSet.Contains);
        bool reordered = !keptInPlaylist.SequenceEqual(keptInRanks, StringComparer.Ordinal);

        return new FavoritesPlan(added, removed, reordered);
    }

    #endregion

}
