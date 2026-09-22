namespace core.monitor;

/// <summary>
/// A single ranked artist or track within a snapshot.
/// </summary>
/// <param name="Rank">1-based position in the top list.</param>
/// <param name="SpotifyId">Spotify's stable id - what entries are matched on across snapshots.</param>
/// <param name="Name">Artist or track name.</param>
/// <param name="Detail">Supporting text: performing artists for a track, genres for an artist.</param>
public record TopEntry(int Rank, string SpotifyId, string Name, string Detail)
{

    public string Display => string.IsNullOrEmpty(Detail) ? Name : $"{Name} - {Detail}";

}
