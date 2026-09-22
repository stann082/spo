namespace core.monitor;

/// <summary>
/// What happened to one artist or track between two snapshots.
/// </summary>
public class EntryChange
{

    #region Properties

    public ChangeKind Kind { get; init; }

    public string SpotifyId { get; init; }

    public string Name { get; init; }

    public string Detail { get; init; }

    /// <summary>Rank in the previous snapshot, or null when the entry is new.</summary>
    public int? PreviousRank { get; init; }

    /// <summary>Rank in the current snapshot, or null when the entry has dropped out.</summary>
    public int? CurrentRank { get; init; }

    /// <summary>Places gained. Positive means it moved up the list; zero for entries that came or went.</summary>
    public int Delta => PreviousRank.HasValue && CurrentRank.HasValue
        ? PreviousRank.Value - CurrentRank.Value
        : 0;

    public string Display => string.IsNullOrEmpty(Detail) ? Name : $"{Name} - {Detail}";

    #endregion

    #region Public Methods

    /// <summary>
    /// A one-line, human-readable description of the change.
    /// </summary>
    /// <param name="listSize">Size of the top list, used to phrase entries dropping out.</param>
    public string Describe(int listSize)
    {
        return Kind switch
        {
            ChangeKind.Entered => $"{Display} entered at #{CurrentRank}",
            ChangeKind.Left => $"{Display} is no longer in the top {listSize} (was #{PreviousRank})",
            ChangeKind.MovedUp => $"{Display} moved up to #{CurrentRank} from #{PreviousRank} (+{Delta})",
            ChangeKind.MovedDown => $"{Display} moved down to #{CurrentRank} from #{PreviousRank} ({Delta})",
            _ => $"{Display} held at #{CurrentRank}"
        };
    }

    #endregion

}
