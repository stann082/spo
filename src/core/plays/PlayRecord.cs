namespace core.plays;

/// <summary>One play of a track, as Spotify's recently-played list reports it.</summary>
/// <param name="PlayedAt">When the play ended, in UTC. Together with the track id it identifies the play.</param>
/// <param name="Artists">The track's artists, comma-separated.</param>
public record PlayRecord(DateTime PlayedAt, string TrackId, string Name, string Artists);

/// <summary>How often one track was played, over whatever period was asked for.</summary>
public record PlayCount(string TrackId, string Name, string Artists, int Plays, DateTime FirstPlayed, DateTime LastPlayed);

/// <summary>The size of the play log as a whole.</summary>
/// <param name="FirstPlayed">The oldest play on record, or null when the log is empty.</param>
public record PlayLogSummary(int Plays, int Tracks, DateTime? FirstPlayed);
