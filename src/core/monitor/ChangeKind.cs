namespace core.monitor;

public enum ChangeKind
{
    /// <summary>Present in both snapshots at the same rank.</summary>
    Held,

    /// <summary>Present in both snapshots, now ranked higher (a smaller rank number).</summary>
    MovedUp,

    /// <summary>Present in both snapshots, now ranked lower (a larger rank number).</summary>
    MovedDown,

    /// <summary>Absent from the previous snapshot.</summary>
    Entered,

    /// <summary>Absent from the current snapshot.</summary>
    Left
}
