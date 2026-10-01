using Newtonsoft.Json;

namespace core.playlists;

/// <summary>
/// How to reorder an existing playlist. Either the whole new order, by id:
/// <code>
/// {
///   "name": "Wet Ceiling",
///   "tracks": [ { "id": "7wGoVO4pK4Kv0mk3tZ2p8w", "title": "STARGAZING", "artist": "Travis Scott" } ]
/// }
/// </code>
/// or a request to keep the order but pull apart tracks by the same artist that sit back to back:
/// <code>
/// { "name": "Wet Ceiling", "spreadArtists": true }
/// </code>
/// "name" is a playlist name or id; "title" and "artist" are only there to make the file readable.
/// </summary>
public class ReorderDefinition
{

    #region Properties

    public string Name { get; set; }

    public bool SpreadArtists { get; set; }

    public List<TrackDefinition> Tracks { get; set; } = [];

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads a reorder file and reports every problem with it at once. Checks that need Spotify
    /// (the file lists exactly what the playlist holds) happen in <see cref="ReorderPlan.ResolveOrder"/>.
    /// </summary>
    public static ReorderDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpoException($"File not found: {path}");
        }

        ReorderDefinition definition;
        try
        {
            definition = JsonConvert.DeserializeObject<ReorderDefinition>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new SpoException($"Could not parse {path}: {ex.Message}");
        }

        if (definition == null)
        {
            throw new SpoException($"{path} is empty.");
        }

        definition.Name = definition.Name?.Trim();
        definition.Tracks ??= [];
        definition.Tracks.RemoveAll(t => t == null);

        var errors = new List<string>();

        if (string.IsNullOrEmpty(definition.Name))
        {
            errors.Add("\"name\" must name the playlist to reorder.");
        }

        if (definition.SpreadArtists && definition.Tracks.Count > 0)
        {
            errors.Add("Give either \"tracks\" (the whole new order) or \"spreadArtists\": true, not both.");
        }
        else if (!definition.SpreadArtists && definition.Tracks.Count == 0)
        {
            errors.Add("Give \"tracks\" (the whole new order) or \"spreadArtists\": true.");
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
