using core.spotify;

namespace core.test;

public class ArtistGenresTests
{

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Genres = new Dictionary<string, IReadOnlyList<string>>
    {
        ["celldweller"] = ["industrial rock", "cyberpunk"],
        ["klayton"] = ["cyberpunk", "industrial"],
        ["quiet"] = []
    };

    [Test]
    public void ForTrack_MergesEveryCreditedArtistWithoutRepeats()
    {
        var genres = ArtistGenres.ForTrack(["celldweller", "klayton"], Genres);

        Assert.That(genres, Is.EqualTo(new[] { "industrial rock", "cyberpunk", "industrial" }));
    }

    [Test]
    public void ForTrack_IgnoresUnknownAndMissingArtists()
    {
        // Local files have artists without ids; Spotify can also omit an artist from a lookup.
        var genres = ArtistGenres.ForTrack([null, "", "unknown", "quiet", "klayton"], Genres);

        Assert.That(genres, Is.EqualTo(new[] { "cyberpunk", "industrial" }));
    }

}
