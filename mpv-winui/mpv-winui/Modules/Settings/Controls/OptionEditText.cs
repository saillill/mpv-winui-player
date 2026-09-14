namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Localized captions for the inline customize-mode row. They live on the row
/// model because WinUI DataTemplates can only bind to the item they are
/// templating, and this keeps the edit card translatable like the rest of the
/// page instead of hard-coding strings in XAML.
/// </summary>
public sealed class OptionEditText
{
    public string DeleteTooltip { get; private set; } = "Hide / restore default";
    public string DeleteAutomationName { get; private set; } = "Delete this entry";
    public string HideCaption { get; private set; } = "Hide from the page (keeps the value)";
    public string ResetCaption { get; private set; } = "Restore default (clears customization and resets this entry)";
    public string MoveUpCaption { get; private set; } = "Move up";
    public string MoveDownCaption { get; private set; } = "Move down";
    public string HideSectionCaption { get; private set; } = "Hide this section";
    public string SectionTooltip { get; private set; } = "Move or hide this section";
    public string DeleteSectionCaption { get; private set; } = "Delete this folder";
    public string MoveToSectionCaption { get; private set; } = "Move into a folder";

    /// <summary>Pulls the current language into the captions.</summary>
    public void Refresh()
    {
        var lang = AppContext.AppLang;
        DeleteTooltip = lang.CustomizeDeleteTip;
        DeleteAutomationName = lang.CustomizeDelete;
        HideCaption = lang.CustomizeHide;
        ResetCaption = lang.CustomizeReset;
        MoveUpCaption = lang.CustomizeMoveUp;
        MoveDownCaption = lang.CustomizeMoveDown;
        HideSectionCaption = lang.CustomizeHideSection;
        SectionTooltip = lang.CustomizeSectionTip;
        DeleteSectionCaption = lang.CustomizeDeleteSection;
        MoveToSectionCaption = lang.CustomizeMoveToSection;
    }
}
