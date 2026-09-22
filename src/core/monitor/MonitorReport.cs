using System.Text;
using core.spotify;

namespace core.monitor;

/// <summary>
/// Turns a run result into text. The full report goes to the log; the short form goes to the toast,
/// which only has room for a handful of lines.
/// </summary>
public static class MonitorReport
{

    #region Public Methods

    /// <summary>The complete report, one line per change, grouped by list.</summary>
    public static IReadOnlyList<string> BuildLines(MonitorRunResult result)
    {
        var lines = new List<string>();

        foreach (var diff in result.Diffs)
        {
            lines.Add($"{Heading(diff)} - {diff.Summarize()}");

            if (diff.IsBaseline)
            {
                lines.Add("  First run: recorded as the baseline, nothing to compare against yet.");
                continue;
            }

            if (!diff.HasChanges)
            {
                lines.Add("  No movement since the last run.");
                continue;
            }

            foreach (var change in diff.Changes)
            {
                lines.Add($"  {change.Describe(diff.ListSize)}");
            }
        }

        return lines;
    }

    /// <summary>The toast headline, e.g. "Spotify: 3 new, 1 out, 12 moved".</summary>
    public static string BuildToastTitle(MonitorRunResult result)
    {
        if (result.IsBaseline)
        {
            return "Spotify: baseline recorded";
        }

        if (!result.HasChanges)
        {
            return result.HasNewLists
                ? "Spotify: started tracking a new list"
                : "Spotify: no change in your top lists";
        }

        int entered = result.Diffs.Sum(d => d.Entered.Count());
        int left = result.Diffs.Sum(d => d.Left.Count());
        int moved = result.Diffs.Sum(d => d.Moved.Count());

        var parts = new List<string>();
        if (entered > 0) parts.Add($"{entered} new");
        if (left > 0) parts.Add($"{left} out");
        if (moved > 0) parts.Add($"{moved} moved");

        return $"Spotify: {string.Join(", ", parts)}";
    }

    /// <summary>
    /// The toast body: the most notable changes across every list, capped at
    /// <paramref name="maxLines"/> so the notification stays readable.
    /// </summary>
    public static string BuildToastBody(MonitorRunResult result, int maxLines = 5)
    {
        if (result.IsBaseline)
        {
            int tracked = result.Diffs.Sum(d => d.ListSize);
            return $"Recorded {tracked} entries across {result.Diffs.Count} lists. Changes will be reported from the next run.";
        }

        if (!result.HasChanges)
        {
            if (result.HasNewLists)
            {
                var newLists = result.Diffs.Where(d => d.IsBaseline).Select(Heading);
                return $"Baseline recorded for {string.Join(", ", newLists)}. Other lists are unchanged.";
            }

            return "Your top artists and tracks are unchanged since the last check.";
        }

        var highlights = result.Diffs
            .SelectMany(diff => diff.Changes.Select(change => new
            {
                Diff = diff,
                Change = change,
                Weight = Weight(change, diff.ListSize)
            }))
            .OrderByDescending(x => x.Weight)
            .Take(maxLines)
            .ToList();

        var body = new StringBuilder();
        foreach (var highlight in highlights)
        {
            body.AppendLine(highlight.Change.Describe(highlight.Diff.ListSize));
        }

        int remaining = result.Diffs.Sum(d => d.Changes.Count) - highlights.Count;
        if (remaining > 0)
        {
            body.Append($"...and {remaining} more");
        }

        return body.ToString().TrimEnd();
    }

    #endregion

    #region Helper Methods

    private static string Heading(TopDiff diff)
    {
        return $"Top {diff.Label} ({SpotifyTimeRange.Describe(diff.TimeRange)})";
    }

    /// <summary>
    /// Ranks changes for the toast. Entering or leaving is treated as more notable than a shuffle,
    /// and anything happening near the top of the list outweighs the same event near the bottom.
    /// </summary>
    private static int Weight(EntryChange change, int listSize)
    {
        return change.Kind switch
        {
            ChangeKind.Entered => listSize + (listSize - change.CurrentRank.Value),
            ChangeKind.Left => listSize + (listSize - change.PreviousRank.Value),
            _ => Math.Abs(change.Delta)
        };
    }

    #endregion

}
