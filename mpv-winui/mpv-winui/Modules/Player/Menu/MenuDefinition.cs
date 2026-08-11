using System.Collections.Generic;

namespace mpv_winui.Modules.Player.Menu;

/// <summary>
/// One entry of the config-driven menu bar. Leaf items run either an app
/// action (<see cref="Action"/>) or a raw mpv command (<see cref="MpvCommand"/>);
/// items with <see cref="Children"/> become submenus.
/// </summary>
public sealed class MenuDefinition
{
    public string? Id { get; set; }

    /// <summary>AppLang property name that holds the localized label.</summary>
    public string? LabelKey { get; set; }

    /// <summary>Optional arguments for format-string labels (e.g. "{0} min").</summary>
    public List<string>? LabelArgs { get; set; }

    /// <summary>Optional FontIcon glyph (Fluent System Icons codepoint).</summary>
    public string? Icon { get; set; }

    /// <summary>Whitelisted app action id (see MpvPlayerPage.ExecuteMenuAction).</summary>
    public string? Action { get; set; }

    /// <summary>Raw mpv command executed when the item is clicked.</summary>
    public string? MpvCommand { get; set; }

    public bool Separator { get; set; }

    public List<MenuDefinition>? Children { get; set; }
}
