using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// Page-side side of the inline customize mode: the toggle in the action bar,
/// and the bridge between <see cref="Controls.OptionListControl"/>'s edit
/// events and the persisted <see cref="Layout.SettingsLayout"/>.
/// </summary>
public sealed partial class SettingsPage
{
    /// <summary>Subscribes to the option list's customize-mode notifications.</summary>
    private void InitCustomizeMode()
    {
        OptionsControl.TextEdited += (option, label, description) =>
            StoreText(option, label, description);

        OptionsControl.RawValueEdited += (option, raw) =>
        {
            // The raw key defaults to whatever the row already publishes, so the
            // user can retype just the value. An empty box drops the override
            // and lets the row's own control drive the option again.
            var key = string.IsNullOrWhiteSpace(option.MpvKey) ? SuggestMpvKey(option) : option.MpvKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                StoreMpvOverride(option, key, raw);
            }
        };

        OptionsControl.HideRequested += option =>
        {
            SetHidden(option, true);
            RebuildLocalizedContent();
        };

        OptionsControl.ResetRequested += option =>
        {
            ResetRowToDefault(option);
            RebuildLocalizedContent();
        };

        OptionsControl.OrderChanged += keys =>
        {
            StoreOrder(keys);
            // Rebuild so the new order is the one the normal (non-customize)
            // view shows when the user leaves the mode.
            RebuildLocalizedContent();
        };

        UpdateCustomizeToggleText();
    }

    private void OnCustomizeToggleClick(object sender, RoutedEventArgs e)
    {
        _customizeMode = CustomizeToggle.IsChecked == true;
        OptionsControl.CustomizeMode = _customizeMode;

        // Section cards are a browse affordance; the flat customizable list is
        // what the mode edits, so hide them while it is on.
        if (_customizeMode)
        {
            SectionsHost.Visibility = Visibility.Collapsed;
            BreadcrumbBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            RebuildLocalizedContent();
        }

        UpdateCustomizeToggleText();
    }

    private void UpdateCustomizeToggleText()
    {
        var lang = AppContext.AppLang;
        CustomizeToggleText.Text = lang.SettingsCustomize;
        ToolTipService.SetToolTip(CustomizeToggle, lang.SettingsCustomizeHint);
    }
}
