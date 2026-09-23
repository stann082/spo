using Newtonsoft.Json;

namespace core.playlists;

/// <summary>
/// Which tracks to move from one existing playlist to another:
/// <code>
/// {
///   "source": "Chase Scene",
///   "target": "House Warm",
///   "tracks": [ { "id": "3yGy1JYz3zQKlxSgjgpQqX", "title": "Praise You", "artist": "Fatboy Slim" } ]
/// }
/// </code>
/// "source" and "target" are playlist names or ids. Tracks are moved by id, so every id must be in
/// the source; "title" and "artist" are only there to make the file readable.
/// </summary>
public class MoveDefinition
{

    #region Properties

    public string Source { get; set; }

    public string Target { get; set; }

    public List<TrackDefinition> Tracks { get; set; } = [];

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads a move file and reports every problem with it at once. Checks that need Spotify
    /// (ids present in the source, what the target already has) happen in <see cref="MovePlan.Build"/>.
    /// </summary>
    public static MoveDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpoException($"File not found: {path}");
        }

        MoveDefinition definition;
        try
        {
            definition = JsonConvert.DeserializeObject<MoveDefinition>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new SpoException($"Could not parse {path}: {ex.Message}");
        }

        if (definition == null)
        {
            throw new SpoException($"{path} is empty.");
        }

        definition.Tracks ??= [];
        definition.Tracks.RemoveAll(t => t == null);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Source))
        {
            errors.Add("\"source\" must name the playlist to move tracks out of.");
        }

        if (string.IsNullOrWhiteSpace(definition.Target))
        {
            errors.Add("\"target\" must name the existing playlist to move tracks into.");
        }

        if (!string.IsNullOrWhiteSpace(definition.Source) && !string.IsNullOrWhiteSpace(definition.Target)
            && string.Equals(definition.Source.Trim(), definition.Target.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("\"source\" and \"target\" are the same playlist.");
        }

        if (definition.Tracks.Count == 0)
        {
            errors.Add("\"tracks\" must list at least one track to move.");
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

        if (errors.Count > 0)
        {
            throw new SpoException($"{path} has problems:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        return definition;
    }

    #endregion

}
