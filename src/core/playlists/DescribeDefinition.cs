using Newtonsoft.Json;

namespace core.playlists;

public class PlaylistDescription
{

    /// <summary>Playlist name or id.</summary>
    public string Name { get; set; }

    /// <summary>New description; optional when <see cref="Rename"/> is given.</summary>
    public string Description { get; set; }

    /// <summary>New name; optional when <see cref="Description"/> is given.</summary>
    public string Rename { get; set; }

}

/// <summary>
/// New descriptions and names for existing playlists:
/// <code>
/// { "playlists": [ { "name": "Smooth Jazz", "rename": "Soft Landing", "description": "Smooth jazz for when..." } ] }
/// </code>
/// </summary>
public class DescribeDefinition
{

    #region Constants

    /// <summary>The longest description Spotify accepts.</summary>
    public const int MaxDescriptionLength = 300;

    /// <summary>The longest playlist name Spotify accepts.</summary>
    public const int MaxNameLength = 100;

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
            playlist.Description = string.IsNullOrWhiteSpace(playlist.Description) ? null : playlist.Description.Trim();
            playlist.Rename = string.IsNullOrWhiteSpace(playlist.Rename) ? null : playlist.Rename.Trim();
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

            bool hasDescription = !string.IsNullOrWhiteSpace(playlist.Description);
            bool hasRename = !string.IsNullOrWhiteSpace(playlist.Rename);

            if (!hasDescription && !hasRename)
            {
                errors.Add($"{label} needs a \"description\", a \"rename\", or both.");
                continue;
            }

            if (hasDescription)
            {
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

            if (hasRename)
            {
                var rename = playlist.Rename.Trim();
                if (rename.Length > MaxNameLength)
                {
                    errors.Add($"{label}: the new name is {rename.Length} characters; Spotify allows {MaxNameLength}.");
                }

                if (rename.Contains('\n') || rename.Contains('\r'))
                {
                    errors.Add($"{label}: the new name has a line break.");
                }
            }
        }

        var repeated = definition.Playlists
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' is listed more than once.");
        errors.AddRange(repeated);

        var sameNewName = definition.Playlists
            .Where(p => !string.IsNullOrWhiteSpace(p.Rename))
            .GroupBy(p => p.Rename.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"More than one playlist would be renamed to '{g.Key}'.");
        errors.AddRange(sameNewName);

        return errors;
    }

    /// <summary>
    /// Renames that would leave two of the user's playlists with the same name. A rename is fine
    /// when the name it takes is being given up by another rename in the same file.
    /// </summary>
    /// <param name="renames">(current name, new name) for every playlist being renamed.</param>
    /// <param name="existingNames">Names of all the user's playlists right now.</param>
    public static List<string> FindNameConflicts(IReadOnlyList<(string current, string wanted)> renames, IEnumerable<string> existingNames)
    {
        var freed = new HashSet<string>(renames.Select(r => r.current.Trim()), StringComparer.OrdinalIgnoreCase);
        var taken = new HashSet<string>(existingNames.Select(n => n.Trim()).Where(n => !freed.Contains(n)), StringComparer.OrdinalIgnoreCase);

        return renames
            .Where(r => !string.Equals(r.current.Trim(), r.wanted.Trim(), StringComparison.OrdinalIgnoreCase) && taken.Contains(r.wanted.Trim()))
            .Select(r => $"Cannot rename '{r.current}' to '{r.wanted}': you already have a playlist with that name.")
            .ToList();
    }

    #endregion

}
