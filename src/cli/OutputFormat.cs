using core;

namespace cli;

public static class OutputFormat
{

    /// <summary>
    /// Normalises a --format value and checks it against what the command supports, so every
    /// command rejects an unknown format the same way instead of printing nothing.
    /// </summary>
    public static string Parse(string value, params string[] supported)
    {
        var format = value?.Trim().ToLowerInvariant();
        if (format == null || !supported.Contains(format))
        {
            throw new SpoException($"Unknown format '{value}'. Use {string.Join(", ", supported[..^1])} or {supported[^1]}.");
        }

        return format;
    }

}
