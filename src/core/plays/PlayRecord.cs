namespace core.plays;

/// <summary>One play of a track, as Spotify's recently-played list reports it.</summary>
/// <param name="PlayedAt">When the play ended, in UTC. Together with the track id it identifies the play.</param>
/// <param name="Artists">The track's artists, comma-separated.</param>
public record PlayRecord(DateTime PlayedAt, string TrackId, string Name, string Artists);

/// <summary>How often one track was played, over whatever period was asked for.</summary>
public record PlayCount(string TrackId, string Name, string Artists, int Plays, DateTime FirstPlayed, DateTime LastPlayed);

/// <summary>
/// A track to count plays for, such as one in a playlist. A play counts towards it when it has
/// the same id, or the same title and lead artist (ignoring case): Spotify gives every release of
/// a song its own id, so the single and the album track are different ids.
/// </summary>
/// <param name="Artists">All the track's artists, comma-separated, as the counts show them.</param>
/// <param name="LeadArtist">The first credited artist, kept apart because an artist's own name can hold a comma.</param>
public record TrackMatch(string Id, string Name, string Artists, string LeadArtist);

/// <summary>The size of the play log as a whole.</summary>
/// <param name="FirstPlayed">The oldest play on record, or null when the log is empty.</param>
public record PlayLogSummary(int Plays, int Tracks, DateTime? FirstPlayed);
