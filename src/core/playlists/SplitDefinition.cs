using Newtonsoft.Json;

namespace core.playlists;

/// <summary>
/// How to split one playlist into several:
/// <code>
/// {
///   "source": "Cybernetic Pulse",
///   "playlists": [
///     { "name": "Old-School EBM", "description": "optional",
///       "tracks": [ { "id": "71OeleUvvObLd3jnN8DhwU", "title": "Headhunter V1.0", "artist": "Front 242" } ] }
///   ]
/// }
/// </code>
/// "source" is a playlist name or id. Tracks are moved by id, so every id must be in the source;
/// "title" and "artist" are only there to make the file readable.
/// </summary>
public class SplitDefinition
{

    #region Properties

    public string Source { get; set; }

    public List<PlaylistDefinition> Playlists { get; set; } = [];

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads a split file and reports every problem with it at once. Checks that need Spotify
    /// (ids present in the source, names not taken) happen in <see cref="SplitPlan.Build"/>.
    /// </summary>
    public static SplitDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpoException($"File not found: {path}");
        }

        SplitDefinition definition;
        try
        {
            definition = JsonConvert.DeserializeObject<SplitDefinition>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new SpoException($"Could not parse {path}: {ex.Message}");
        }

        if (definition == null)
        {
            throw new SpoException($"{path} is empty.");
        }

        definition.Playlists ??= [];
        definition.Playlists.RemoveAll(p => p == null);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Source))
        {
            errors.Add("\"source\" must name the playlist to split.");
        }

        if (definition.Playlists.Count == 0)
        {
            errors.Add("\"playlists\" must list at least one playlist to create.");
        }

        for (int i = 0; i < definition.Playlists.Count; i++)
        {
            var playlist = definition.Playlists[i];
            playlist.Tracks ??= [];
            playlist.Tracks.RemoveAll(t => t == null);

            if (string.IsNullOrWhiteSpace(playlist.Name))
            {
                errors.Add($"Playlist #{i + 1} needs a \"name\".");
                continue;
            }

            if (playlist.Tracks.Count == 0)
            {
                errors.Add($"'{playlist.Name}' has no tracks.");
            }

            var missingIds = playlist.Tracks
                .Select((track, index) => (track, number: index + 1))
                .Where(x => string.IsNullOrWhiteSpace(x.track.Id))
                .Select(x => $"#{x.number}")
                .ToList();

            if (missingIds.Count > 0)
            {
                errors.Add($"'{playlist.Name}': every track needs an \"id\" (missing on {string.Join(", ", missingIds)}).");
            }
        }

        var repeatedNames = definition.Playlists
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}'");

        foreach (var name in repeatedNames)
        {
            errors.Add($"{name} is listed more than once.");
        }

        if (errors.Count > 0)
        {
            throw new SpoException($"{path} has problems:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        return definition;
    }

    #endregion

}
