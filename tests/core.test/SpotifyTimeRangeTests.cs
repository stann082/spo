using core.spotify;
using SpotifyAPI.Web;

namespace core.test;

public class SpotifyTimeRangeTests
{

    [TestCase("short", PersonalizationTopRequest.TimeRange.ShortTerm)]
    [TestCase("medium", PersonalizationTopRequest.TimeRange.MediumTerm)]
    [TestCase("long", PersonalizationTopRequest.TimeRange.LongTerm)]
    public void Parse_MapsEveryKnownRange(string range, PersonalizationTopRequest.TimeRange expected)
    {
        Assert.That(SpotifyTimeRange.Parse(range), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_RejectsATypoInsteadOfFallingBackToMedium()
    {
        var ex = Assert.Throws<SpoException>(() => SpotifyTimeRange.Parse("shrot"));

        Assert.That(ex.Message, Does.Contain("shrot"));
    }

}
