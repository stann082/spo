using core.export;
using Newtonsoft.Json.Linq;
using SpotifyAPI.Web;

namespace core.test;

public class JsonExportTests
{

    #region Helper Methods

    private static FullTrack Track()
    {
        return new FullTrack
        {
            Id = "t1",
            Uri = "spotify:track:t1",
            Name = "Headhunter",
            DurationMs = 283000,
            ExternalIds = new Dictionary<string, string> { ["isrc"] = "BEA018800001" },
            Artists = [new SimpleArtist { Id = "a1", Name = "Front 242" }],
            Album = new SimpleAlbum { Id = "al1", Name = "Front by Front", AlbumType = "album", ReleaseDate = "1988-09-05" }
        };
    }

    private static JObject Parse(object value)
    {
        return JObject.Parse(JsonOutput.Serialize(value));
    }

    #endregion

    #region Tests

    [Test]
    public void TrackRecord_UsesCamelCaseAndCarriesTheAlbumYear()
    {
        var json = Parse(TrackRecord.From(Track()));

        Assert.Multiple(() =>
        {
            Assert.That((string)json["name"], Is.EqualTo("Headhunter"));
            Assert.That((string)json["artists"]![0]!["name"], Is.EqualTo("Front 242"));
            Assert.That((string)json["album"]!["albumType"], Is.EqualTo("album"));
            Assert.That((string)json["album"]!["year"], Is.EqualTo("1988"));
            Assert.That((string)json["isrc"], Is.EqualTo("BEA018800001"));
            Assert.That((int)json["durationMs"], Is.EqualTo(283000));
        });
    }

    [Test]
    public void TrackRecord_LeavesOutWhatWasNotAskedFor()
    {
        var json = Parse(TrackRecord.From(Track()));

        // No genres requested, not from a playlist, not from history: none of those keys appear.
        Assert.Multiple(() =>
        {
            Assert.That(json.ContainsKey("genres"), Is.False);
            Assert.That(json.ContainsKey("addedAt"), Is.False);
            Assert.That(json.ContainsKey("playedAt"), Is.False);
        });
    }

    [Test]
    public void TrackRecord_WithGenresRequested_AlwaysHasTheKeyEvenWhenEmpty()
    {
        var json = Parse(TrackRecord.From(Track(), genresByArtist: new Dictionary<string, IReadOnlyList<string>>()));

        Assert.That(json["genres"], Is.InstanceOf<JArray>().And.Empty);
    }

    [Test]
    public void TrackRecord_WritesTimestampsAsUtcIso8601()
    {
        var playedAt = new DateTime(2026, 9, 22, 14, 30, 0, DateTimeKind.Utc);

        var serialized = JsonOutput.Serialize(TrackRecord.From(Track(), playedAt: playedAt));

        Assert.That(serialized, Does.Contain("\"playedAt\": \"2026-09-22T14:30:00Z\""));
    }

    [Test]
    public void Serialize_EscapesNonAsciiSoAnyConsoleCodePageKeepsItIntact()
    {
        var track = Track();
        track.Artists = [new SimpleArtist { Id = "a2", Name = "Leæther Strip" }];

        var serialized = JsonOutput.Serialize(TrackRecord.From(track));

        Assert.Multiple(() =>
        {
            Assert.That(serialized, Does.Contain("Le\\u00e6ther Strip"));
            Assert.That((string)JObject.Parse(serialized)["artists"]![0]!["name"], Is.EqualTo("Leæther Strip"));
        });
    }

    [Test]
    public void TrackRecord_WithoutAnIsrcOrAlbumDate_OmitsThem()
    {
        var track = Track();
        track.ExternalIds = null;
        track.Album.ReleaseDate = null;

        var json = Parse(TrackRecord.From(track));

        Assert.Multiple(() =>
        {
            Assert.That(json.ContainsKey("isrc"), Is.False);
            Assert.That(((JObject)json["album"]!).ContainsKey("year"), Is.False);
        });
    }

    [Test]
    public void PlaylistRecord_DecodesTheDescriptionSpotifyReturnsEscaped()
    {
        var json = Parse(PlaylistRecord.From(new SimplePlaylist
        {
            Id = "p1",
            Name = "Soft Landing",
            Description = "Dave Koz &amp; friends &#x27;til dawn",
            Public = true
        }));

        Assert.Multiple(() =>
        {
            Assert.That((string)json["description"], Is.EqualTo("Dave Koz & friends 'til dawn"));
            Assert.That((bool)json["public"], Is.True);
        });
    }

    [Test]
    public void PlaylistRecord_WithoutADescription_OmitsIt()
    {
        var json = Parse(PlaylistRecord.From(new SimplePlaylist { Id = "p1", Name = "Pop2K", Description = "" }));

        Assert.That(json.ContainsKey("description"), Is.False);
    }

    #endregion

}
