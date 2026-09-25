using CommandLine;

namespace cli.options;

[Verb("playlists", HelpText = "List, search and create playlists. With no options, lists your playlists.")]
public class PlaylistsOptions
{

    [Option('c', "create", SetName = "create", MetaValue = "FILE", HelpText = "Create a playlist from a JSON file: { name, description, tracks: [{ id } | { title, artist } | { title, artist, status }] }. Tracks with an id are added directly, tracks with a status (e.g. \"blocked\") are skipped and reported, all others are searched by title/artist.")]
    public string CreateFile { get; set; }

    [Option("add", SetName = "add", MetaValue = "FILE", HelpText = "Add tracks to an existing playlist from a JSON file in the --create format; \"name\" picks the playlist. Tracks already in it are skipped.")]
    public string AddFile { get; set; }

    [Option("split", SetName = "split", MetaValue = "FILE", HelpText = "Move tracks out of a playlist into new ones, as described by a JSON file: { source, playlists: [{ name, description, tracks: [{ id }] }] }. Each new playlist is created and filled before its tracks are removed from the source; unclaimed tracks stay behind.")]
    public string SplitFile { get; set; }

    [Option("move", SetName = "move", MetaValue = "FILE", HelpText = "Move tracks from one existing playlist to another, as described by a JSON file: { source, target, tracks: [{ id }] }. Tracks are added to the target (unless already there) before they are removed from the source.")]
    public string MoveFile { get; set; }

    [Option("remove", SetName = "remove", MetaValue = "FILE", HelpText = "Remove tracks from an existing playlist, from a JSON file in the --create format; \"name\" picks the playlist. Tracks are removed by id only, and every id must be in the playlist.")]
    public string RemoveFile { get; set; }

    [Option("describe", SetName = "describe", MetaValue = "FILE", HelpText = "Set the descriptions of existing playlists from a JSON file: { playlists: [{ name, description }] }. \"name\" is a playlist name or id; descriptions already as given are left alone.")]
    public string DescribeFile { get; set; }

    [Option("mark-orphans", SetName = "split", HelpText = "With --split, rename the source to '<name>_orphaned tracks' and make it private if any tracks stay behind.")]
    public bool MarkOrphans { get; set; }

    [Option("dry-run", HelpText = "With --create, --add, --split, --move, --remove or --describe, show what would happen without changing anything.")]
    public bool DryRun { get; set; }

    [Option('q', "query", SetName = "list", HelpText = "Only playlists whose name contains this text (case-insensitive).")]
    public string Query { get; set; }

    [Option("show-tracks", SetName = "list", HelpText = "List the tracks of each matching playlist as [Song],[Artists],[Album],[Year].")]
    public bool ShowTracks { get; set; }

    [Option("show-genres", SetName = "list", HelpText = "With --show-tracks, add the genres of each track's artists. Costs one extra request per 50 distinct artists.")]
    public bool ShowGenres { get; set; }

    [Option("show-track-id", SetName = "list", HelpText = "With --show-tracks, append each track's id. JSON output always includes ids.")]
    public bool ShowTrackId { get; set; }

    [Option("format", SetName = "list", Default = "text", HelpText = "Output format: text or json. JSON nests each playlist's tracks and adds album type, duration, ISRC and date added.")]
    public string Format { get; set; }

}
