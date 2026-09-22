namespace core.playlists;

/// <summary>
/// What a playlist file turns into once the searches are done: the URIs to add, in the order the
/// file lists them, plus what could not be found and what was skipped on purpose.
/// </summary>
public class PlaylistImportResult
{

    #region Properties

    public IReadOnlyList<string> Uris { get; private init; } = [];

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
        var uris = new List<string>();
        var notFound = new List<TrackDefinition>();
        int requested = 0;

        foreach (var track in definition.Tracks)
        {
            switch (track.Source)
            {
                case TrackSource.Id:
                    requested++;
                    uris.Add(track.Uri);
                    break;

                case TrackSource.Search:
                    requested++;
                    if (searchHits.TryGetValue(track, out var uri) && !string.IsNullOrEmpty(uri))
                    {
                        uris.Add(uri);
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
            Uris = uris,
            Requested = requested,
            NotFound = notFound,
            Skipped = definition.Tracks
                .Where(t => t.Source == TrackSource.Skipped)
                .GroupBy(t => t.Status.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    #endregion

}
