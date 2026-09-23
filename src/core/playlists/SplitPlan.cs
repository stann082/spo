namespace core.playlists;

/// <summary>One item in the playlist being split, as far as the plan cares.</summary>
/// <param name="Uri">spotify:track:…, or spotify:local:… / spotify:episode:… for items that cannot be moved by id.</param>
public record SourceItem(string Uri, string Display);

/// <param name="Uris">Tracks to add, in file order, without repeats.</param>
public record SplitTarget(PlaylistDefinition Definition, IReadOnlyList<string> Uris);

/// <summary>
/// A validated split: what each new playlist gets, and what stays behind in the source. Built
/// entirely before anything changes, so a bad file or a rerun fails without side effects.
/// </summary>
public class SplitPlan
{

    #region Constants

    public const string OrphanedSuffix = "_orphaned tracks";

    #endregion

    #region Properties

    public IReadOnlyList<SplitTarget> Targets { get; private init; } = [];

    /// <summary>Source items no target claims, in source order. They stay in the source playlist.</summary>
    public IReadOnlyList<SourceItem> Orphans { get; private init; } = [];

    #endregion

    #region Public Methods

    /// <param name="definition">The split file.</param>
    /// <param name="sourceName">Name of the playlist being split, for messages.</param>
    /// <param name="source">Everything currently in the source playlist.</param>
    /// <param name="existingPlaylistNames">Names of all the user's playlists, to refuse creating a second one with the same name.</param>
    public static SplitPlan Build(
        SplitDefinition definition,
        string sourceName,
        IReadOnlyList<SourceItem> source,
        IEnumerable<string> existingPlaylistNames)
    {
        var sourceUris = new HashSet<string>(source.Select(s => s.Uri), StringComparer.Ordinal);
        var existing = new HashSet<string>(existingPlaylistNames.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var targets = new List<SplitTarget>();

        foreach (var playlist in definition.Playlists)
        {
            if (existing.Contains(playlist.Name.Trim()))
            {
                errors.Add($"A playlist named '{playlist.Name}' already exists. Rename it in the file, or remove the existing one.");
            }

            var notInSource = playlist.Tracks
                .Where(t => !sourceUris.Contains(t.Uri))
                .Select(t => string.IsNullOrWhiteSpace(t.Title) ? t.Id : $"{t} ({t.Id})")
                .ToList();

            if (notInSource.Count > 0)
            {
                errors.Add($"'{playlist.Name}' lists track(s) that are not in '{sourceName}': {string.Join("; ", notInSource)}");
            }

            targets.Add(new SplitTarget(playlist, playlist.Tracks.Select(t => t.Uri).Distinct(StringComparer.Ordinal).ToList()));
        }

        if (errors.Count > 0)
        {
            throw new SpoException($"The split cannot run:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        var claimed = new HashSet<string>(targets.SelectMany(t => t.Uris), StringComparer.Ordinal);

        return new SplitPlan
        {
            Targets = targets,
            Orphans = source.Where(s => !claimed.Contains(s.Uri)).ToList()
        };
    }

    public static string OrphanedName(string sourceName)
    {
        return $"{sourceName}{OrphanedSuffix}";
    }

    #endregion

}
