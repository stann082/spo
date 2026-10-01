using core.playlists;

namespace core.test;

public class ReorderPlanTests
{

    #region Helper Methods

    private static readonly IReadOnlyList<PlaylistEntry> Items =
    [
        new("spotify:track:a", "A", ["One"]),
        new("spotify:track:b", "B", ["Two"]),
        new("spotify:track:c", "C", ["Three"]),
        new("spotify:track:a", "A again", ["One"])
    ];

    private static ReorderDefinition Definition(params string[] ids)
    {
        return new ReorderDefinition { Name = "Mix", Tracks = ids.Select(id => new TrackDefinition { Id = id }).ToList() };
    }

    private static List<int> Applied(ReorderPlan plan)
    {
        return ReorderPlan.Apply(Enumerable.Range(0, plan.Order.Count), plan.Moves);
    }

    #endregion

    #region Build Tests

    [Test]
    public void Build_NeedsNoMovesForTheCurrentOrder()
    {
        var plan = ReorderPlan.Build([0, 1, 2, 3]);

        Assert.That(plan.Moves, Is.Empty);
    }

    [Test]
    public void Build_MovesOneTrackWithOneMoveHoweverFarItGoes()
    {
        var toTheEnd = ReorderPlan.Build([0, 2, 3, 4, 5, 1]);
        var toTheStart = ReorderPlan.Build([5, 0, 1, 2, 3, 4]);

        Assert.Multiple(() =>
        {
            Assert.That(toTheEnd.Moves, Is.EqualTo(new[] { new ReorderMove(1, 6, 1) }));
            Assert.That(toTheStart.Moves, Is.EqualTo(new[] { new ReorderMove(5, 0, 5) }));
            Assert.That(Applied(toTheEnd), Is.EqualTo(toTheEnd.Order));
            Assert.That(Applied(toTheStart), Is.EqualTo(toTheStart.Order));
        });
    }

    [Test]
    public void Build_MovesGiveTheWantedOrderWithAsFewMovesAsPossible()
    {
        // 1, 4 and 6 are already in order relative to each other, so only the other four move.
        var plan = ReorderPlan.Build([3, 1, 0, 4, 6, 5, 2]);

        Assert.Multiple(() =>
        {
            Assert.That(Applied(plan), Is.EqualTo(plan.Order));
            Assert.That(plan.Moves, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void Build_MovesGiveTheWantedOrderForShuffledPlaylists()
    {
        var random = new Random(1987);

        for (int run = 0; run < 200; run++)
        {
            var order = Enumerable.Range(0, random.Next(1, 40)).OrderBy(_ => random.Next()).ToList();

            var plan = ReorderPlan.Build(order);

            Assert.That(Applied(plan), Is.EqualTo(order), $"order {string.Join(",", order)}");
        }
    }

    [Test]
    public void Build_RefusesAnOrderThatIsNotEveryPositionOnce()
    {
        Assert.Throws<ArgumentException>(() => ReorderPlan.Build([0, 0, 2]));
        Assert.Throws<ArgumentException>(() => ReorderPlan.Build([0, 3]));
    }

    #endregion

    #region ResolveOrder Tests

    [Test]
    public void ResolveOrder_MapsTheFileToCurrentPositions()
    {
        // A track that is in the playlist twice takes its copies in playlist order.
        var order = ReorderPlan.ResolveOrder(Definition("c", "a", "spotify:track:b", "a"), "Mix", Items);

        Assert.That(order, Is.EqualTo(new[] { 2, 0, 1, 3 }));
    }

    [Test]
    public void ResolveOrder_RefusesTracksThatAreNotInThePlaylist()
    {
        var definition = Definition("a", "b", "c", "a", "a");
        definition.Tracks.Add(new TrackDefinition { Id = "gone", Title = "Gone", Artist = "Someone" });

        var ex = Assert.Throws<SpoException>(() => ReorderPlan.ResolveOrder(definition, "Mix", Items));

        Assert.That(ex.Message, Does.Contain("not in 'Mix'").And.Contain("; Gone — Someone (gone)"));
    }

    [Test]
    public void ResolveOrder_RefusesAFileThatLeavesTracksOut()
    {
        var ex = Assert.Throws<SpoException>(() => ReorderPlan.ResolveOrder(Definition("b", "a"), "Mix", Items));

        Assert.That(ex.Message, Does.Contain("must list every track of 'Mix'; missing: C; A again"));
    }

    #endregion

}
