namespace core.monitor;

/// <summary>What the monitor did, or would do on a dry run, to the favorites playlist.</summary>
public class FavoritesSyncResult
{

    public string PlaylistName { get; init; }

    /// <summary>True when the playlist did not exist and was created (or would be, on a dry run).</summary>
    public bool Created { get; init; }

    public FavoritesPlan Plan { get; init; }

    public int TrackCount { get; init; }

    public bool DryRun { get; init; }

    /// <summary>Set when the sync failed; the snapshots are saved regardless.</summary>
    public string Error { get; init; }

    public string Describe()
    {
        if (Error != null)
        {
            return $"Playlist '{PlaylistName}' was not updated: {Error}";
        }

        string would = DryRun ? "would be " : "";

        if (Created)
        {
            return $"Playlist '{PlaylistName}' {would}created with your top {TrackCount} tracks.";
        }

        if (Plan.IsUnchanged)
        {
            return $"Playlist '{PlaylistName}' already matches your top {TrackCount} tracks.";
        }

        var parts = new List<string>();
        if (Plan.Added.Count > 0) parts.Add($"{Plan.Added.Count} added");
        if (Plan.Removed.Count > 0) parts.Add($"{Plan.Removed.Count} removed");
        if (Plan.Reordered) parts.Add("reordered");

        return $"Playlist '{PlaylistName}' {would}updated: {string.Join(", ", parts)} ({TrackCount} tracks).";
    }

}
