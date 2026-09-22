using Newtonsoft.Json;

namespace core.playlists;

/// <summary>
/// A playlist described in a JSON file:
/// <code>
/// {
///   "name": "Road Trip",
///   "description": "optional",
///   "tracks": [
///     { "id": "4uLU6hMCjMI75M1A2tKUQC" },
///     { "title": "Paranoid", "artist": "Black Sabbath" },
///     { "title": "Some Song", "artist": "Someone", "status": "blocked" }
///   ]
/// }
/// </code>
/// </summary>
public class PlaylistDefinition
{

    #region Properties

    public string Name { get; set; }

    public string Description { get; set; }

    public List<TrackDefinition> Tracks { get; set; } = [];

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads and validates a playlist file. Every problem is reported before anything touches
    /// Spotify, so a bad file never leaves a half-built playlist behind.
    /// </summary>
    public static PlaylistDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpoException($"File not found: {path}");
        }

        PlaylistDefinition definition;
        try
        {
            definition = JsonConvert.DeserializeObject<PlaylistDefinition>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new SpoException($"Could not parse {path}: {ex.Message}");
        }

        if (definition == null || string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new SpoException($"{path} must specify a playlist \"name\".");
        }

        definition.Tracks ??= [];
        definition.Tracks.RemoveAll(t => t == null);

        var invalid = definition.Tracks
            .Select((track, index) => (track, number: index + 1))
            .Where(x => string.IsNullOrWhiteSpace(x.track.Id) && string.IsNullOrWhiteSpace(x.track.Title))
            .Select(x => $"#{x.number}")
            .ToList();

        if (invalid.Count > 0)
        {
            throw new SpoException($"Every track needs an \"id\" or a \"title\". Missing on track(s) {string.Join(", ", invalid)} in {path}.");
        }

        return definition;
    }

    #endregion

}
