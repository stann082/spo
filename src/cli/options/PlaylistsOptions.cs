using CommandLine;

namespace cli.options;

[Verb("playlists", HelpText = "List, search and create playlists. With no options, lists your playlists.")]
public class PlaylistsOptions
{

    [Option('c', "create", SetName = "create", MetaValue = "FILE", HelpText = "Create a playlist from a JSON file: { name, description, tracks: [{ id } | { title, artist } | { title, artist, status }] }. Tracks with an id are added directly, tracks with a status (e.g. \"blocked\") are skipped and reported, all others are searched by title/artist.")]
    public string CreateFile { get; set; }

    [Option('q', "query", SetName = "list", HelpText = "Only playlists whose name contains this text (case-insensitive).")]
    public string Query { get; set; }

    [Option("show-tracks", SetName = "list", HelpText = "List the tracks of each matching playlist as [Song],[Artists],[Album],[Year].")]
    public bool ShowTracks { get; set; }

    [Option("show-track-id", SetName = "list", HelpText = "With --show-tracks, append each track's id.")]
    public bool ShowTrackId { get; set; }

}
