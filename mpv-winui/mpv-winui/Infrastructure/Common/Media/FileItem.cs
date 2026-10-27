namespace mpv_winui.Infrastructure.Common.Media;

/// <summary>
/// What a path handed to the player actually is.
/// </summary>
public enum FileType
{
    File,
    Folder,
    Url,
    Other,
}

/// <summary>
/// One thing to open: a path plus what it is.
///
/// These two types used to live in <c>Modules/Player/MpvPlayerExtensions.cs</c>,
/// next to the mpv open commands. But the protocol-activation service builds them
/// from a <c>mpvw://</c> URI and never opens anything, so it had to import the
/// player module to describe a path it was already holding. They carry no player
/// state and no mpv types — only a string and an enum — so they sit below both.
/// </summary>
public record FileItem(string Path, FileType Type = FileType.File);