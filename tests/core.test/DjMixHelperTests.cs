using core.dj;
using SpotifyAPI.Web;

namespace core.test;

public class DjMixHelperTests
{

    #region Helper Methods

    private static (FullTrack track, TrackAudioFeatures features) Track(string id, float tempo, int key = 0, int mode = 1)
    {
        return (new FullTrack { Id = id, Name = id }, new TrackAudioFeatures { Id = id, Tempo = tempo, Key = key, Mode = mode });
    }

    #endregion

    #region Tests

    [Test]
    public void OrderForDjMix_WithUnknownTempos_DoesNotThrow()
    {
        // The legacy version indexed the filtered tempo list by the unfiltered count and threw here.
        var items = new List<(FullTrack, TrackAudioFeatures)>
        {
            Track("a", 0), Track("b", 0), Track("c", 120)
        };

        Assert.That(() => DjMixHelper.OrderForDjMix(items), Has.Count.EqualTo(3));
    }

    [Test]
    public void OrderForDjMix_WithNoKnownTempos_KeepsEveryTrack()
    {
        var items = new List<(FullTrack, TrackAudioFeatures)> { Track("a", 0), Track("b", 0) };

        Assert.That(DjMixHelper.OrderForDjMix(items), Has.Count.EqualTo(2));
    }

    [Test]
    public void OrderForDjMix_SeedsWithTheMedianTempoTrack()
    {
        var items = new List<(FullTrack, TrackAudioFeatures)>
        {
            Track("slow", 80), Track("mid", 120), Track("fast", 170)
        };

        Assert.That(DjMixHelper.OrderForDjMix(items)[0].track.Id, Is.EqualTo("mid"));
    }

    [Test]
    public void OrderForDjMix_PrefersAHarmonicNeighbourOverAClash()
    {
        // Seed is the median tempo, in C major (8B). G major (9B) is adjacent on the wheel, F# major
        // (2B) is opposite; both are within 6% tempo of the seed, so key alone decides.
        var items = new List<(FullTrack, TrackAudioFeatures)>
        {
            Track("clash", 122, key: 6),
            Track("seed", 120, key: 0),
            Track("neighbour", 118, key: 7)
        };

        var order = DjMixHelper.OrderForDjMix(items).Select(x => x.track.Id).ToList();

        Assert.That(order, Is.EqualTo(new[] { "seed", "neighbour", "clash" }));
    }

    #endregion

}
