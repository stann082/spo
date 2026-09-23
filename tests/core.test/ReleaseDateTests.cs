using core.spotify;

namespace core.test;

public class ReleaseDateTests
{

    [TestCase("1988", "1988")]
    [TestCase("1988-05", "1988")]
    [TestCase("1988-05-12", "1988")]
    [TestCase(" 2013-10-15 ", "2013")]
    [TestCase("0000", "")]
    [TestCase("", "")]
    [TestCase(null, "")]
    [TestCase("unknown", "")]
    public void Year_HandlesEveryPrecisionSpotifyUses(string releaseDate, string expected)
    {
        Assert.That(ReleaseDate.Year(releaseDate), Is.EqualTo(expected));
    }

}
