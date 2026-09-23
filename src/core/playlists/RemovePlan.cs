namespace core.playlists;

/// <summary>
/// A validated removal from an existing playlist, from a file in the --create format. Tracks are
/// removed by id only: a search could match a different track than the one in the playlist.
/// Built entirely before anything changes, so a bad file or a rerun fails without side effects.
/// </summary>
public class RemovePlan
{

    #region Properties

    /// <summary>Tracks to remove, in file order, without repeats.</summary>
    public IReadOnlyList<SourceItem> Removing { get; private init; } = [];

    #endregion

    #region Public Methods

    /// <param name="definition">The remove file; "name" picks the playlist.</param>
    /// <param name="playlistName">Name of the playlist, for messages.</param>
    /// <param name="items">Everything currently in the playlist.</param>
    public static RemovePlan Build(PlaylistDefinition definition, string playlistName, IReadOnlyList<SourceItem> items)
    {
        var errors = new List<string>();

        if (definition.Tracks.Count == 0)
        {
            errors.Add("\"tracks\" must list at least one track to remove.");
        }

        var missingIds = definition.Tracks
            .Select((track, index) => (track, number: index + 1))
            .Where(x => string.IsNullOrWhiteSpace(x.track.Id))
            .Select(x => $"#{x.number}")
            .ToList();

        if (missingIds.Count > 0)
        {
            errors.Add($"Every track needs an \"id\" (missing on {string.Join(", ", missingIds)}).");
        }

        var itemsByUri = items
            .GroupBy(s => s.Uri, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var withIds = definition.Tracks.Where(t => !string.IsNullOrWhiteSpace(t.Id)).ToList();

        var notInPlaylist = withIds
            .Where(t => !itemsByUri.ContainsKey(t.Uri))
            .Select(t => string.IsNullOrWhiteSpace(t.Title) ? t.Id : $"{t} ({t.Id})")
            .ToList();

        if (notInPlaylist.Count > 0)
        {
            errors.Add($"Track(s) not in '{playlistName}': {string.Join("; ", notInPlaylist)}");
        }

        if (errors.Count > 0)
        {
            throw new SpoException($"The removal cannot run:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        return new RemovePlan
        {
            Removing = withIds
                .Select(t => t.Uri)
                .Distinct(StringComparer.Ordinal)
                .Select(uri => itemsByUri[uri])
                .ToList()
        };
    }

    #endregion

}
