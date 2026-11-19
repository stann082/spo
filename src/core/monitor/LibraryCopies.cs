using System.Text.RegularExpressions;

namespace core.monitor;

/// <summary>A track in one of the user's own playlists.</summary>
/// <param name="LeadArtist">The first credited artist.</param>
public record LibraryTrack(string Id, string Name, string LeadArtist);

/// <summary>
/// Finds the user's own copy of a song. Spotify gives every release its own id, so the top lists
/// can name the radio edit or the single while the user's playlists hold the album track. The
/// monitor playlists use the copy the user already has, and only fall back to Spotify's when the
/// song is in none of their playlists.
/// </summary>
public class LibraryCopies
{

    #region Constructors

    /// <param name="tracks">The user's playlist tracks; when a song is in several, the first one wins.</param>
    public LibraryCopies(IEnumerable<LibraryTrack> tracks)
    {
        foreach (var track in tracks.Where(t => !string.IsNullOrEmpty(t?.Id)))
        {
            _ids.Add(track.Id);
            _byTitle.TryAdd(Key(track.Name, track.LeadArtist), track.Id);
            _bySong.TryAdd(Key(SongTitle(track.Name), track.LeadArtist), track.Id);
        }
    }

    #endregion

    #region Variables

    // The tags a release adds to a title without making it a different recording or arrangement.
    // Remixes, live and acoustic versions are left alone: they are different songs to the listener.
    private static readonly Regex VersionTag = new(
        @"\s*(?:-\s*|\(|\[)(?:(?:radio|clean|explicit|single|album|original|short|uncensored|censored)(?:\s+(?:edit|version|mix))?|edit|(?:\d{4}\s+)?(?:digital(?:ly)?\s+)?remaster(?:ed)?(?:\s+\d{4})?(?:\s+version)?|mono|stereo)[\)\]]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Featured artists, which releases credit differently: "(feat. Travis Scott)", "(with Gunna & ft. Travis Scott)".
    private static readonly Regex FeatureTag = new(
        @"\s*[\(\[](?:feat\.?|ft\.?|featuring|with|and)\s[^\)\]]*[\)\]]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byTitle = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bySong = new(StringComparer.Ordinal);

    #endregion

    #region Public Methods

    /// <summary>
    /// The id of the user's copy of this track: the track itself when they have it, else the one
    /// with the same title and lead artist, else the same song under another release's title (such
    /// as "Shake That - Radio Edit" for "Shake That"). Null when they have no copy.
    /// </summary>
    public string Find(string id, string name, string leadArtist)
    {
        if (!string.IsNullOrEmpty(id) && _ids.Contains(id))
        {
            return id;
        }

        if (_byTitle.TryGetValue(Key(name, leadArtist), out var sameTitle))
        {
            return sameTitle;
        }

        return _bySong.TryGetValue(Key(SongTitle(name), leadArtist), out var sameSong) ? sameSong : null;
    }

    /// <summary>
    /// The title without release tags or featured artists: "Shake That - Radio Edit" is "Shake That",
    /// "Turks (feat. Travis Scott)" is "Turks".
    /// </summary>
    public static string SongTitle(string name)
    {
        string title = name ?? string.Empty;
        string previous;
        do
        {
            previous = title;
            title = FeatureTag.Replace(VersionTag.Replace(title, string.Empty), string.Empty);
        }
        while (title != previous && title.Length > 0);

        // A title that is nothing but a tag stays as it was.
        return title.Length > 0 ? title.Trim() : (name ?? string.Empty).Trim();
    }

    #endregion

    #region Helper Methods

    private static string Key(string name, string leadArtist)
    {
        return $"{(name ?? string.Empty).Trim().ToLowerInvariant()}\n{(leadArtist ?? string.Empty).Trim().ToLowerInvariant()}";
    }

    #endregion

}
