using core;
using core.playlists;
using core.spotify;
using SpotifyAPI.Web;

namespace cli.commands;

/// <summary>
/// Turns the tracks of a playlist file into Spotify URIs, shared by --create and --add. Every
/// search hit is printed with its album and year, because search takes the first result and
/// that can be a remix, a re-recording or a compilation cut; seeing it is how you catch that.
/// </summary>
public static class TrackFileResolver
{

    #region Constants

    /// <summary>Searches in flight at once.</summary>
    private const int SearchConcurrency = 5;

    #endregion

    #region Public Methods

    public static async Task<PlaylistImportResult> ResolveAsync(ISpotifyClient spotify, PlaylistDefinition definition)
    {
        var toSearch = definition.Tracks.Where(t => t.Source == TrackSource.Search).ToList();
        if (toSearch.Count > 0)
        {
            Console.WriteLine($"Searching for {toSearch.Count} track(s)...");
        }

        var hits = await Batching.MapAsync(toSearch, SearchConcurrency, async (track, token) =>
        {
            var response = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, track.SearchQuery), token);
            return response.Tracks?.Items?.FirstOrDefault();
        });

        foreach (var (track, hit) in toSearch.Zip(hits).Where(pair => pair.Second != null))
        {
            var artists = string.Join(", ", hit.Artists.Select(a => a.Name));
            Console.WriteLine($"  {track}  =>  {hit.Name} — {artists} ({hit.Album?.Name}, {ReleaseDate.Year(hit.Album?.ReleaseDate)}) [{hit.Id}]");
        }

        var searchHits = toSearch
            .Zip(hits)
            .ToDictionary(pair => pair.First, pair => pair.Second?.Uri);

        return PlaylistImportResult.Assemble(definition, searchHits);
    }

    /// <summary>What could not be found and what was skipped on purpose.</summary>
    public static void ReportLeftovers(PlaylistImportResult result)
    {
        if (result.NotFound.Count > 0)
        {
            Console.WriteLine("Could not find the following track(s):");
            foreach (var track in result.NotFound)
            {
                Console.WriteLine($"  {track}");
            }
        }

        foreach (var group in result.Skipped)
        {
            Console.WriteLine($"Skipped ({group.Key}):");
            foreach (var track in group)
            {
                Console.WriteLine($"  {track}");
            }
        }
    }

    #endregion

}
