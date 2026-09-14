namespace mpv_winui.Modules.Settings.Controls;

/// <summary>
/// Localized captions for the inline customize-mode row. They live on the row
/// model because WinUI DataTemplates can only bind to the item they are
/// templating, and this keeps the edit card translatable like the rest of the
/// page instead of hard-coding strings in XAML.
/// </summary>
public sealed class OptionEditText
{
    public string NameCaption { get; private set; } = "Name";
    public string DescriptionCaption { get; private set; } = "Description";
    public string DescriptionPlaceholder { get; private set; } = "(leave empty to hide the description)";
    public string RawCaption { get; private set; } = "Raw mpv key / value";
    public string RawPlaceholder { get; private set; } = "raw mpv option value, e.g. d3d11va";
    public string DeleteTooltip { get; private set; } = "Hide / restore default";
    public string DeleteAutomationName { get; private set; } = "Delete this entry";
    public string HideCaption { get; private set; } = "Hide from the page (keeps the value)";
    public string ResetCaption { get; private set; } = "Restore default (clears customization and resets this entry)";

    /// <summary>Pulls the current language into the captions.</summary>
    public void Refresh()
    {
        var lang = AppContext.AppLang;
        NameCaption = lang.CustomizeFieldName;
        DescriptionCaption = lang.CustomizeFieldDescription;
        DescriptionPlaceholder = lang.CustomizeFieldDescriptionHint;
        RawCaption = lang.CustomizeFieldRaw;
        RawPlaceholder = lang.CustomizeFieldRawHint;
        DeleteTooltip = lang.CustomizeDeleteTip;
        DeleteAutomationName = lang.CustomizeDelete;
        HideCaption = lang.CustomizeHide;
        ResetCaption = lang.CustomizeReset;
    }
}
