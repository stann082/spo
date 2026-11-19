using core.monitor;

namespace core.test;

public class LibraryCopiesTests
{

    #region Tests

    [Test]
    public void Find_PrefersTheTrackItself()
    {
        var copies = new LibraryCopies([new LibraryTrack("album", "Walk", "Pantera"), new LibraryTrack("single", "Walk", "Pantera")]);

        Assert.That(copies.Find("single", "Walk", "Pantera"), Is.EqualTo("single"));
    }

    [Test]
    public void Find_UsesTheCopyWithTheSameTitleAndLeadArtist()
    {
        var copies = new LibraryCopies([new LibraryTrack("album", "New Rules", "Dua Lipa")]);

        Assert.That(copies.Find("single", "new rules", "DUA LIPA"), Is.EqualTo("album"));
    }

    [Test]
    public void Find_UsesTheCopyOfTheSameSongUnderAnotherReleasesTitle()
    {
        var copies = new LibraryCopies(
        [
            new LibraryTrack("ringtone-era", "Shake That", "Eminem"),
            new LibraryTrack("remastered", "Paranoid - 2012 Remaster", "Black Sabbath"),
            new LibraryTrack("plain", "Papa Was A Rollin' Stone", "The Temptations")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(copies.Find("clean", "Shake That - Radio Edit", "Eminem"), Is.EqualTo("ringtone-era"));
            Assert.That(copies.Find("original", "Paranoid", "Black Sabbath"), Is.EqualTo("remastered"));
            Assert.That(copies.Find("single", "Papa Was A Rollin' Stone (Single Version)", "The Temptations"), Is.EqualTo("plain"));
        });
    }

    [Test]
    public void Find_DoesNotTreatRemixesLiveVersionsOrOtherArtistsAsTheSameSong()
    {
        var copies = new LibraryCopies([new LibraryTrack("album", "Blinding Lights", "The Weeknd")]);

        Assert.Multiple(() =>
        {
            Assert.That(copies.Find("remix", "Blinding Lights - Chromatics Remix", "The Weeknd"), Is.Null);
            Assert.That(copies.Find("live", "Blinding Lights - Live", "The Weeknd"), Is.Null);
            Assert.That(copies.Find("cover", "Blinding Lights", "Someone Else"), Is.Null);
        });
    }

    [Test]
    public void Find_TakesTheFirstCopyWhenSeveralPlaylistsHaveTheSong()
    {
        var copies = new LibraryCopies([new LibraryTrack("first", "Shake That", "Eminem"), new LibraryTrack("second", "Shake That", "Eminem")]);

        Assert.That(copies.Find("clean", "Shake That - Radio Edit", "Eminem"), Is.EqualTo("first"));
    }

    [Test]
    public void SongTitle_StripsReleaseTagsButKeepsTheRest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LibraryCopies.SongTitle("Shake That - Radio Edit"), Is.EqualTo("Shake That"));
            Assert.That(LibraryCopies.SongTitle("Hey Jude - Remastered 2015"), Is.EqualTo("Hey Jude"));
            Assert.That(LibraryCopies.SongTitle("Song - 2011 Remaster - Clean"), Is.EqualTo("Song"));
            Assert.That(LibraryCopies.SongTitle("Go (Explicit Version)"), Is.EqualTo("Go"));
            Assert.That(LibraryCopies.SongTitle("Turks (with Gunna & ft. Travis Scott)"), Is.EqualTo("Turks"));
            Assert.That(LibraryCopies.SongTitle("Turks (feat. Travis Scott)"), Is.EqualTo("Turks"));
            Assert.That(LibraryCopies.SongTitle("Pound Town (and Tay Keith)"), Is.EqualTo("Pound Town"));
            Assert.That(LibraryCopies.SongTitle("Best Part (feat. H.E.R.) - Radio Edit"), Is.EqualTo("Best Part"));
            Assert.That(LibraryCopies.SongTitle("(Can't You) Trip Like I Do"), Is.EqualTo("(Can't You) Trip Like I Do"));
            Assert.That(LibraryCopies.SongTitle("Edit"), Is.EqualTo("Edit"));
            Assert.That(LibraryCopies.SongTitle("Prayer in C - Robin Schulz Radio Edit"), Is.EqualTo("Prayer in C - Robin Schulz Radio Edit"));
        });
    }

    #endregion

}
