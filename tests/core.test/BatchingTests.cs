namespace core.test;

public class BatchingTests
{

    #region Tests

    [Test]
    public async Task ForEachChunkAsync_RespectsTheChunkSizeAndOrder()
    {
        var chunks = new List<int[]>();

        await Batching.ForEachChunkAsync(Enumerable.Range(1, 120), 50, chunk =>
        {
            chunks.Add(chunk);
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(c => c.Length), Is.EqualTo(new[] { 50, 50, 20 }));
            Assert.That(chunks.SelectMany(c => c), Is.EqualTo(Enumerable.Range(1, 120)));
        });
    }

    [Test]
    public async Task MapAsync_ReturnsResultsInInputOrder()
    {
        var items = Enumerable.Range(1, 40).ToList();

        // Later items finish first, so any ordering bug would show.
        var results = await Batching.MapAsync(items, 8, async (item, token) =>
        {
            await Task.Delay(40 - item, token);
            return item * 10;
        });

        Assert.That(results, Is.EqualTo(items.Select(i => i * 10)));
    }

    [Test]
    public async Task MapAsync_NeverExceedsMaxConcurrency()
    {
        int inFlight = 0;
        int peak = 0;

        await Batching.MapAsync(Enumerable.Range(1, 30).ToList(), 4, async (item, token) =>
        {
            int now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref peak, now);
            await Task.Delay(10, token);
            Interlocked.Decrement(ref inFlight);
            return item;
        });

        Assert.That(peak, Is.InRange(1, 4));
    }

    [Test]
    public void MapAsync_RejectsZeroConcurrency()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Batching.MapAsync(new[] { 1 }, 0, (item, _) => Task.FromResult(item)));
    }

    #endregion

    #region Helper Methods

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }

    #endregion

}
