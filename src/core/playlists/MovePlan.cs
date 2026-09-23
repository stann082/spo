namespace core.playlists;

/// <summary>
/// A validated move between two existing playlists. Built entirely before anything changes, so a
/// bad file or a rerun fails without side effects.
/// </summary>
public class MovePlan
{

    #region Properties

    /// <summary>Every track that leaves the source, in file order, without repeats.</summary>
    public IReadOnlyList<SourceItem> Moving { get; private init; } = [];

    /// <summary>The part of <see cref="Moving"/> the target does not have yet.</summary>
    public IReadOnlyList<SourceItem> ToAdd { get; private init; } = [];

    /// <summary>The part of <see cref="Moving"/> already in the target: only removed from the source.</summary>
    public IReadOnlyList<SourceItem> AlreadyInTarget { get; private init; } = [];

    #endregion

    #region Public Methods

    /// <param name="definition">The move file.</param>
    /// <param name="sourceName">Name of the source playlist, for messages.</param>
    /// <param name="source">Everything currently in the source playlist.</param>
    /// <param name="targetUris">Everything currently in the target playlist.</param>
    public static MovePlan Build(
        MoveDefinition definition,
        string sourceName,
        IReadOnlyList<SourceItem> source,
        IEnumerable<string> targetUris)
    {
        var sourceByUri = source
            .GroupBy(s => s.Uri, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var notInSource = definition.Tracks
            .Where(t => !sourceByUri.ContainsKey(t.Uri))
            .Select(t => string.IsNullOrWhiteSpace(t.Title) ? t.Id : $"{t} ({t.Id})")
            .ToList();

        if (notInSource.Count > 0)
        {
            throw new SpoException($"The move cannot run: track(s) not in '{sourceName}': {string.Join("; ", notInSource)}");
        }

        var moving = definition.Tracks
            .Select(t => t.Uri)
            .Distinct(StringComparer.Ordinal)
            .Select(uri => sourceByUri[uri])
            .ToList();

        var inTarget = new HashSet<string>(targetUris, StringComparer.Ordinal);

        return new MovePlan
        {
            Moving = moving,
            ToAdd = moving.Where(m => !inTarget.Contains(m.Uri)).ToList(),
            AlreadyInTarget = moving.Where(m => inTarget.Contains(m.Uri)).ToList()
        };
    }

    #endregion

}
