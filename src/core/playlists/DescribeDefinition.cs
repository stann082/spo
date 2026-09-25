using Newtonsoft.Json;

namespace core.playlists;

public class PlaylistDescription
{

    /// <summary>Playlist name or id.</summary>
    public string Name { get; set; }

    public string Description { get; set; }

}

/// <summary>
/// New descriptions for existing playlists:
/// <code>
/// { "playlists": [ { "name": "Soft Landing", "description": "Smooth jazz for when..." } ] }
/// </code>
/// </summary>
public class DescribeDefinition
{

    #region Constants

    /// <summary>The longest description Spotify accepts.</summary>
    public const int MaxDescriptionLength = 300;

    #endregion

    #region Properties

    public List<PlaylistDescription> Playlists { get; set; } = [];

    #endregion

    #region Public Methods

    /// <summary>Reads a describe file and reports every problem with it at once.</summary>
    public static DescribeDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpoException($"File not found: {path}");
        }

        DescribeDefinition definition;
        try
        {
            definition = JsonConvert.DeserializeObject<DescribeDefinition>(File.ReadAllText(path));
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

        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new SpoException($"{path} has problems:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", errors)}");
        }

        foreach (var playlist in definition.Playlists)
        {
            playlist.Name = playlist.Name.Trim();
            playlist.Description = playlist.Description.Trim();
        }

        return definition;
    }

    public static List<string> Validate(DescribeDefinition definition)
    {
        var errors = new List<string>();

        if (definition.Playlists.Count == 0)
        {
            errors.Add("\"playlists\" must list at least one playlist.");
        }

        for (int i = 0; i < definition.Playlists.Count; i++)
        {
            var playlist = definition.Playlists[i];
            var label = string.IsNullOrWhiteSpace(playlist.Name) ? $"Playlist #{i + 1}" : $"'{playlist.Name.Trim()}'";

            if (string.IsNullOrWhiteSpace(playlist.Name))
            {
                errors.Add($"{label} needs a \"name\".");
            }

            if (string.IsNullOrWhiteSpace(playlist.Description))
            {
                errors.Add($"{label} needs a \"description\".");
                continue;
            }

            var description = playlist.Description.Trim();
            if (description.Length > MaxDescriptionLength)
            {
                errors.Add($"{label}: the description is {description.Length} characters; Spotify allows {MaxDescriptionLength}.");
            }

            if (description.Contains('\n') || description.Contains('\r'))
            {
                errors.Add($"{label}: the description has a line break; Spotify descriptions are a single line.");
            }
        }

        var repeated = definition.Playlists
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' is listed more than once.");
        errors.AddRange(repeated);

        return errors;
    }

    #endregion

}
