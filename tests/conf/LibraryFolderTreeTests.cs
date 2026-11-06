using mpv_winui.Domains.Playback.Cards;
using NUnit.Framework;

namespace mpv_conf_test;

/// <summary>
/// How the video library turns a flat list of paths into folders to walk through.
///
/// <para>The scan recurses, so a path already carries the tree. What needs pinning is
/// which folders appear: only the next level, a folder that holds no video of its own but
/// leads to one still appears, and the count on a folder card counts only what is inside
/// it rather than everything below it.</para>
/// </summary>
[TestFixture]
public class LibraryFolderTreeTests
{
    private static readonly string[] Library =
    [
        @"D:\Media\Trips\2024\a.mp4",
        @"D:\Media\Trips\2024\b.mp4",
        @"D:\Media\Trips\notes\c.mp4",
        @"D:\Media\Shows\d.mp4",
        @"D:\Media\e.mp4",
    ];

    [Test]
    public void TheTopLevelListsOneStepDownOnly()
    {
        var children = LibraryFolderTree.Children(Library, @"D:\Media");

        Assert.That(
            string.Join("|", children.Select(f => f.Name)),
            Is.EqualTo("Shows|Trips"),
            "only the next level: 2024 and notes are inside Trips, not here");
    }

    [Test]
    public void TheTopLevelIsTheLibraryRootNotTheDrive()
    {
        // Reported from a real run: the folder tab opened showing "C:" as a folder to
        // walk into. The drive is not part of the library, and opening it walks out of
        // the library entirely.
        var children = LibraryFolderTree.Children(Library, string.Empty);

        Assert.That(
            string.Join("|", children.Select(f => f.Name)),
            Is.EqualTo("Media"),
            @"the top level is the folder the library is BOUND to (D:\Media here), not the drive");

        Assert.That(
            children.Single().Count,
            Is.EqualTo(1),
            "its count is e.mp4, the one video directly inside it");
    }

    [Test]
    public void ALibraryBoundToTwoUnrelatedFoldersListsBoth()
    {
        // A real library can hold D:\Media and C:\Clips at once. Their common ancestor is
        // nothing useful - the nearest one is the drive - so the top level has to be the
        // two bound folders themselves, not a wrapper above them.
        var spread = new[]
        {
            @"D:\Media\a.mp4",
            @"C:\Clips\b.mp4",
        };

        var children = LibraryFolderTree.Children(spread, string.Empty);

        Assert.That(
            string.Join("|", children.Select(f => f.Name)),
            Is.EqualTo("Clips|Media"),
            "two roots, name order, no drive between them");
    }

    [Test]
    public void AFolderThatOnlyLeadsSomewhereStillAppears()
    {
        // Trips holds no video of its own; everything under it is one level deeper. It
        // still has to appear, or there is no way to walk into it.
        var children = LibraryFolderTree.Children(Library, @"D:\Media");

        var trips = children.Single(f => f.Name == "Trips");

        Assert.That(trips.Count, Is.EqualTo(0),
            "the count is what is DIRECTLY inside: Trips holds nested folders, not videos");

        // And the same rule where the two answers DIFFER, which is the case the count
        // above cannot distinguish: Media holds one video of its own and three below it.
        // Counting the subtree would say four.
        var media = LibraryFolderTree.Children(Library, @"D:").Single(f => f.Name == "Media");

        Assert.That(media.Count, Is.EqualTo(1),
            "e.mp4 is directly in Media; a, b, c and d are one level down or more");
    }

    [Test]
    public void TheCountCountsWhatIsInsideNotWhatIsBelow()
    {
        var children = LibraryFolderTree.Children(Library, @"D:\Media\Trips");

        Assert.Multiple(() =>
        {
            Assert.That(children.Single(f => f.Name == "2024").Count, Is.EqualTo(2),
                "a.mp4 and b.mp4 are in 2024; c.mp4 is not");
            Assert.That(children.Single(f => f.Name == "notes").Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void OpeningAFolderListsItsOwnVideos()
    {
        var videos = LibraryFolderTree.VideosIn(Library, @"D:\Media\Trips\2024");

        Assert.That(
            string.Join("|", videos.Select(System.IO.Path.GetFileName)),
            Is.EqualTo("a.mp4|b.mp4"),
            "name order, and only this folder's files");
    }

    [Test]
    public void AVideoFolderHoldsBothFilesAndFolders()
    {
        var children = LibraryFolderTree.Children(Library, @"D:\Media\Trips\2024");

        Assert.That(children, Is.Empty, "2024 holds no sub-folders");

        var videos = LibraryFolderTree.VideosIn(Library, @"D:\Media\Trips\2024");

        Assert.That(videos, Has.Count.EqualTo(2));
    }

    [Test]
    public void TheTrailRunsFromTheLibraryRootDown()
    {
        var trail = LibraryFolderTree.Trail(Library, @"D:\Media\Trips\2024");

        Assert.That(
            string.Join(" > ", trail.Select(f => f.Name)),
            Is.EqualTo("Media > Trips > 2024"),
            "the drive is not part of the library, so the trail starts at the folder the videos are under");
    }

    [Test]
    public void TheTrailStartsAtTheBoundFolderNotAtTheUsersFolder()
    {
        // Reported from a real run: inside a bound folder the trail read
        // Users\z2637\AppData\Local\Temp\mpvw-vidlib - the whole ancestry above the
        // library, which nobody bound and nobody wants to click.
        var spread = new[]
        {
            @"C:\Users\someone\AppData\Local\Temp\mpvw-vidlib\Trips\a.mp4",
        };

        var trail = LibraryFolderTree.Trail(
            spread,
            @"C:\Users\someone\AppData\Local\Temp\mpvw-vidlib\Trips",
            root: @"C:\Users\someone\AppData\Local\Temp\mpvw-vidlib");

        Assert.That(
            string.Join(" > ", trail.Select(f => f.Name)),
            Is.EqualTo("mpvw-vidlib > Trips"),
            "the trail starts where the library starts");
    }

    [Test]
    public void TheTrailOfTheRootIsJustItself()
    {
        var trail = LibraryFolderTree.Trail(Library, @"D:\Media");

        Assert.That(string.Join(" > ", trail.Select(f => f.Name)), Is.EqualTo("Media"));
    }

    [Test]
    public void NoPathsMeansNoFolders()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LibraryFolderTree.Children([], @"D:\Media"), Is.Empty);
            Assert.That(LibraryFolderTree.VideosIn([], @"D:\Media"), Is.Empty);
            Assert.That(LibraryFolderTree.Trail([], @"D:\Media"), Is.Empty);
        });
    }

    [Test]
    public void CaseIsIgnoredWhenMatchingPaths()
    {
        // Windows paths are case-insensitive, and a library assembled from a case-changing
        // tool would otherwise show the same folder twice.
        var videos = LibraryFolderTree.VideosIn(Library, @"d:\media\TRIPS\2024");

        Assert.That(videos, Has.Count.EqualTo(2));
    }
}
