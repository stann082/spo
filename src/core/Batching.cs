namespace core;

/// <summary>
/// The two ways spo splits up work, kept apart on purpose: chunk size is an API request limit,
/// concurrency is how many calls are in flight. They are never the same knob.
/// </summary>
public static class Batching
{

    #region Public Methods

    /// <summary>
    /// Calls <paramref name="action"/> once per chunk of at most <paramref name="chunkSize"/>
    /// items, one chunk at a time and in order.
    /// </summary>
    public static async Task ForEachChunkAsync<T>(
        IEnumerable<T> items,
        int chunkSize,
        Func<T[], Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        foreach (var chunk in items.Chunk(chunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(chunk);
        }
    }

    /// <summary>
    /// Runs <paramref name="selector"/> for every item with at most
    /// <paramref name="maxConcurrency"/> calls in flight, and returns the results in input order.
    /// Each call returns its own result instead of writing to a shared collection, so callers
    /// never need locks.
    /// </summary>
    public static async Task<TResult[]> MapAsync<T, TResult>(
        IReadOnlyList<T> items,
        int maxConcurrency,
        Func<T, CancellationToken, Task<TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);

        var results = new TResult[items.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, items.Count), options, async (index, token) =>
        {
            results[index] = await selector(items[index], token);
        });

        return results;
    }

    #endregion

}
