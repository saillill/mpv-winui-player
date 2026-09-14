using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Settings.Layout;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        // 2nd-level (section / column) editing. Captions are localized, so they
        // are resolved to a stable id before being stored. User-created folders
        // have no caption in AppLang, so they resolve by their own name.
        OptionsControl.SectionMoveRequested += (caption, delta) =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                MoveSection(id, delta);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.SectionHideRequested += caption =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                SetSectionHidden(id, true);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.SectionDeleteRequested += caption =>
        {
            if (ResolveSectionId(caption) is { } id)
            {
                DeleteSection(id);
                RebuildLocalizedContent();
            }
        };

        OptionsControl.MoveRowRequested += (optionKey, sectionId) =>
        {
            MoveRowToSection(optionKey, sectionId);
            RebuildLocalizedContent();
        };

        OptionsControl.CreateSectionRequested += async () => await CreateSectionInteractiveAsync();

        UpdateCustomizeToggleText();
    }

    /// <summary>
    /// Resolves a section caption to its stable id, covering the folders the
    /// user created themselves (which have no AppLang caption to look up).
    /// </summary>
    private string? ResolveSectionId(string? caption)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return null;
        }

        if (SettingsSectionIds.IdFor(caption) is { } builtIn)
        {
            return builtIn;
        }

        return _layout.CustomSections
            .FirstOrDefault(s => string.Equals(s.Name, caption, StringComparison.Ordinal))
            ?.Id;
    }

    /// <summary>Asks for a name and creates a 2nd-level folder in this category.</summary>
    private async Task CreateSectionInteractiveAsync()
    {
        if (CurrentCategoryKey is not { } categoryKey)
        {
            return;
        }

        var lang = AppContext.AppLang;
        var input = new TextBox
        {
            PlaceholderText = lang.CustomizeNewSection,
            Header = lang.CustomizeNewSection,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = lang.CustomizeNewSection,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    input,
                    new TextBlock
                    {
                        Text = lang.CustomizeNewSectionHint,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    },
                },
            },
            PrimaryButtonText = lang.CustomizeNewSection,
            CloseButtonText = lang.Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        CreateSection(categoryKey, input.Text);
        RebuildLocalizedContent();
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

        // Rebuild either way: entering the mode is what attaches the pane's own
        // edit menus, and leaving it is what drops them again.
        RebuildLocalizedContent();
        UpdateCustomizeToggleText();
    }

    private void UpdateCustomizeToggleText()
    {
        var lang = AppContext.AppLang;
        CustomizeToggleText.Text = lang.SettingsCustomize;
        ToolTipService.SetToolTip(CustomizeToggle, lang.SettingsCustomizeHint);
    }

    // ===== sidebar (category pane) commands =====

    /// <summary>Attaches the sidebar edit menu to a pane item while customizing.</summary>
    internal void ApplyCategoryEditMenu(NavigationViewItem item, string categoryKey, int index)
    {
        if (!_customizeMode)
        {
            return;
        }

        var lang = AppContext.AppLang;

        var moveUp = new MenuFlyoutItem { Text = lang.CustomizeMoveUp };
        moveUp.Click += (_, _) => { MoveCategory(index, -1); RebuildLocalizedContent(); };

        var moveDown = new MenuFlyoutItem { Text = lang.CustomizeMoveDown };
        moveDown.Click += (_, _) => { MoveCategory(index, 1); RebuildLocalizedContent(); };

        var hide = new MenuFlyoutItem { Text = lang.CustomizeHideCategory };
        hide.Click += (_, _) => { SetCategoryHidden(categoryKey, true); RebuildLocalizedContent(); };

        var flyout = new MenuFlyout();
        flyout.Items.Add(moveUp);
        flyout.Items.Add(moveDown);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(hide);

        // Right-click and the context-menu key both open it; the flyout is
        // rebuilt per rebuild so the captured index stays correct.
        item.ContextFlyout = flyout;
    }

    /// <summary>Restores every sidebar category the customize mode hid.</summary>
    internal void RestoreHiddenCategories()
    {
        _layout.HiddenCategories.Clear();
        SaveLayout();
    }

    // ===== sidebar drag reorder =====

    /// <summary>Index in the pane of the item currently being dragged.</summary>
    private int _draggingCategoryIndex = -1;

    /// <summary>
    /// Attaches hand-written drag-to-reorder to a pane entry. NavigationView
    /// has no built-in item drag, so the gesture is done with the item's own
    /// pointer events: press captures the index, release over another entry
    /// commits the move. Only active while customizing, where the pane is a
    /// flat list of categories.
    /// </summary>
    private void AttachCategoryDrag(NavigationViewItem item, int index)
    {
        if (!_customizeMode)
        {
            return;
        }

        var start = new Windows.Foundation.Point();
        var captured = false;

        item.PointerPressed += (_, e) =>
        {
            start = e.GetCurrentPoint(item).Position;
            captured = true;
        };

        // PointerReleased only fires on the item the press started on, so the
        // drop target is found from the pointer's own position instead.
        item.PointerReleased += (_, e) =>
        {
            if (!captured)
            {
                return;
            }
            captured = false;

            var end = e.GetCurrentPoint(item).Position;
            if (Math.Abs(end.Y - start.Y) < 12 && Math.Abs(end.X - start.X) < 12)
            {
                // A click, not a drag: let the pane's own selection run.
                return;
            }

            var target = CategoryIndexAtPoint(e.GetCurrentPoint(CategoryNav).Position);
            if (target >= 0)
            {
                _draggingCategoryIndex = index;
                MoveCategoryTo(index, target);
                RebuildLocalizedContent();
            }
        };
    }

    /// <summary>Index of the pane entry under a point in the pane's space.</summary>
    private int CategoryIndexAtPoint(Windows.Foundation.Point point)
    {
        for (var i = 0; i < CategoryNav.MenuItems.Count; i++)
        {
            if (CategoryNav.MenuItems[i] is not NavigationViewItem item)
            {
                continue;
            }

            var origin = item.TransformToVisual(CategoryNav)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            var bounds = new Windows.Foundation.Rect(origin, item.RenderSize);
            if (bounds.Contains(point))
            {
                return i;
            }
        }
        return -1;
    }
}
