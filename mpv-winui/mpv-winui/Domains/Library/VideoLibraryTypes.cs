using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Domains.Playback;
using System.Collections.Generic;

namespace mpv_winui.Domains.Library;


/// <summary>
/// How the video library orders its list. Session-only: it is a way of
/// looking at the list during this run, not a stored preference, so it has
/// no AppSettings property and never reaches mpv.conf.
/// </summary>
public enum VideoSortMode
{
    NameAsc,
    NameDesc,
    Newest,
    Oldest,
}

/// <summary>
/// One folder in the video library's "Video folders" tab: the directory and
/// the played videos that live directly in it. Built from the watch history
/// alone, so a group exists because something in the folder was played, not
/// because the folder was found on disk. <see cref="CountText"/> is a string
/// so the row template can bind it to a TextBlock without a conversion.
/// </summary>
public sealed record VideoFolderGroup(
    string Name,
    IReadOnlyList<LibraryEntry> Items,
    string Path = "",
    string Letter = "")
{
    /// <summary>How many entries the group holds, as text for the row template.</summary>
    public string CountText => Items.Count.ToString();

    /// <summary>
    /// The folder's cover: the thumbnail of its first video.
    ///
    /// Reported as 视频库应显示大路径 + 分栏文件夹图片样式. There was no image anywhere in the
    /// folder view -- measured, the automation tree held zero ImageControl elements there,
    /// only the header text and the video cards underneath.
    ///
    /// The first item's thumbnail rather than an invented folder icon: the point of a cover
    /// in a video library is to be recognisable at a glance, and a frame from a video watched
    /// in that folder does that where a generic glyph does not. The image is already loaded
    /// by the card path, so this costs a reference, not a second decode.
    ///
    /// Null is a designed state: a group whose videos all lack thumbnails shows the folder
    /// glyph, which is what the header does today.
    /// </summary>
    public ImageSource? Cover => Items.Count > 0 ? Items[0].Thumbnail : null;

    /// <summary>Hides the cover element while there is nothing to put in it.</summary>
    public Visibility CoverVisibility =>
        Cover is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// True when the group has a real directory to show.
    ///
    /// Music groups are built from a tag and have no path, so their header must
    /// not reserve the grey second line for an empty string - that would indent
    /// every album row away from the video ones for no reason.
    /// </summary>
    public Visibility PathVisibility =>
        string.IsNullOrWhiteSpace(Path) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// The header's glyph: a folder when the group is a real directory, an album
    /// when it is a tag.
    ///
    /// A path STRING plus a Geometry property, following LibraryEntry - see the
    /// lifetime rule on FluentIcons. A Geometry may belong to exactly one PathIcon
    /// for its life, and these groups are rebuilt on every section switch, so a
    /// cached one would be handed to a second icon and the header would drop its
    /// glyph after the first visit.
    /// </summary>
    public string MediaIconPath =>
        string.IsNullOrWhiteSpace(Path)
            ? Modules.Common.View.FluentIcons.Album
            : Modules.Common.View.FluentIcons.Folder;

    /// <summary>A fresh Geometry per access; see <see cref="MediaIconPath"/>.</summary>
    public Geometry MediaIcon => Modules.Common.View.FluentIcons.Geometry(MediaIconPath);
}
