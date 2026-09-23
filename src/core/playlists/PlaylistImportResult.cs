namespace core.playlists;

/// <summary>
/// What a playlist file turns into once the searches are done: the URIs to add, in the order the
/// file lists them, plus what could not be found and what was skipped on purpose.
/// </summary>
public class PlaylistImportResult
{

    #region Properties

    public IReadOnlyList<string> Uris => Resolved.Select(r => r.Uri).ToList();

    /// <summary>Each track that resolved to a URI, paired with the file entry it came from, in file order.</summary>
    public IReadOnlyList<ResolvedTrack> Resolved { get; private init; } = [];

    /// <summary>Tracks that were meant to be added: everything except the skipped ones.</summary>
    public int Requested { get; private init; }

    public IReadOnlyList<TrackDefinition> NotFound { get; private init; } = [];

    /// <summary>Skipped tracks grouped by status, compared case-insensitively.</summary>
    public IReadOnlyList<IGrouping<string, TrackDefinition>> Skipped { get; private init; } = [];

    #endregion

    #region Public Methods

    /// <param name="definition">The playlist file.</param>
    /// <param name="searchHits">
    /// For each <see cref="TrackSource.Search"/> track, the URI Spotify search found, or null when
    /// it found nothing.
    /// </param>
    public static PlaylistImportResult Assemble(PlaylistDefinition definition, IReadOnlyDictionary<TrackDefinition, string> searchHits)
    {
        var resolved = new List<ResolvedTrack>();
        var notFound = new List<TrackDefinition>();
        int requested = 0;

        foreach (var track in definition.Tracks)
        {
            switch (track.Source)
            {
                case TrackSource.Id:
                    requested++;
                    resolved.Add(new ResolvedTrack(track, track.Uri));
                    break;

                case TrackSource.Search:
                    requested++;
                    if (searchHits.TryGetValue(track, out var uri) && !string.IsNullOrEmpty(uri))
                    {
                        resolved.Add(new ResolvedTrack(track, uri));
                    }
                    else
                    {
                        notFound.Add(track);
                    }
                    break;
            }
        }

        return new PlaylistImportResult
        {
            Resolved = resolved,
            Requested = requested,
            NotFound = notFound,
            Skipped = definition.Tracks
                .Where(t => t.Source == TrackSource.Skipped)
                .GroupBy(t => t.Status.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    /// <summary>
    /// Splits the resolved tracks into those to add to an existing playlist and those it already
    /// has. A track listed twice in the file (or two entries resolving to the same track) is
    /// added once; the repeat is reported as already there.
    /// </summary>
    public AddSelection ExcludeExisting(IEnumerable<string> existingUris)
    {
        var present = new HashSet<string>(existingUris, StringComparer.Ordinal);
        var toAdd = new List<ResolvedTrack>();
        var alreadyThere = new List<ResolvedTrack>();

        foreach (var track in Resolved)
        {
            // HashSet.Add is false when the URI was already in the playlist or already queued.
            (present.Add(track.Uri) ? toAdd : alreadyThere).Add(track);
        }

        return new AddSelection(toAdd, alreadyThere);
    }

    #endregion

}

public record ResolvedTrack(TrackDefinition Track, string Uri);

public record AddSelection(IReadOnlyList<ResolvedTrack> ToAdd, IReadOnlyList<ResolvedTrack> AlreadyThere);
