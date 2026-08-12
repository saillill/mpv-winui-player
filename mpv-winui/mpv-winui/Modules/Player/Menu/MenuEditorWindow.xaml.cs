using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mpv_winui.Modules.Common.View;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Graphics;

namespace mpv_winui.Modules.Player.Menu;

/// <summary>Row wrapper for the editor list (glyph + label).</summary>
public sealed record MenuEntryRow(string Glyph, string Label, MenuDefinition Definition, bool IsSubmenu)
{
    public string Glyph { get; } = Glyph;
    public string Label { get; } = Label;
    public MenuDefinition Definition { get; } = Definition;
    public bool IsSubmenu { get; } = IsSubmenu;
}

/// <summary>
/// Standalone editor for the user menu override (menus.json in the mpv config
/// directory). Navigation-style: the list shows the current level's entries,
/// double-click enters a submenu, breadcrumb "↑" goes back. Field edits write
/// back to the in-memory definition; Save serializes the tree to disk.
/// </summary>
public sealed partial class MenuEditorWindow : Window
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(MenuEditorWindow));

    private List<MenuDefinition> _root = [];
    private readonly Stack<(List<MenuDefinition> List, string Name)> _path = [];

    public MenuEditorWindow()
    {
        InitializeComponent();
        AppWindow.Title = "Menu Editor";
        AppWindow.SetIcon("App.ico");
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new SizeInt32(920, 620));

        // Action choices come from the registry (built statically with the page).
        ActionBox.ItemsSource = MpvPlayerPage.KnownMenuActions;

        Reload();
        RefreshPath();
    }

    private string TargetPath => MenuDefinitionSource.UserPath;

    private static string BundledPath => MenuDefinitionSource.BundledPath;

    private void Reload()
    {
        try
        {
            _root = LoadRoot();
            _path.Clear();
            RefreshList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "menu editor reload failed");
        }
    }

    private List<MenuDefinition> LoadRoot()
    {
        if (!File.Exists(TargetPath))
        {
            // Seed the user override from the bundled default so the editor
            // always edits the user file (never the read-only bundled one).
            if (File.Exists(BundledPath))
            {
                File.Copy(BundledPath, TargetPath, overwrite: true);
            }
        }
        var json = File.ReadAllText(TargetPath);
        return JsonSerializer.Deserialize(json, MenuJsonContext.Default.ListMenuDefinition) ?? [];
    }

    private List<MenuDefinition> CurrentList => _path.Count > 0 ? _path.Peek().List : _root;

    private void RefreshList()
    {
        ItemList.ItemsSource = null;
        ItemList.ItemsSource = CurrentList
            .Select(e => new MenuEntryRow(
                e.Separator ? "\u00AF" : e.Children is { Count: > 0 } ? "\uE8B7" : "\uE70F",
                e.Separator ? "———————" : (ResolveLabel(e) ?? e.Id ?? "(no label)"),
                e,
                e.Children is { Count: > 0 }))
            .ToList();
        RefreshPath();
    }

    private void RefreshPath()
    {
        var crumbs = _path.Reverse().Select(p => p.Name).ToList();
        PathText.Text = crumbs.Count > 0 ? "/" + string.Join(" / ", crumbs) : "/";
    }

    private static string? ResolveLabel(MenuDefinition def)
    {
        if (def.LabelKey is not { } key)
        {
            return null;
        }
        var text = typeof(mpv_winui.Modules.Language.AppLang).GetProperty(key)?.GetValue(AppContext.AppLang) as string ?? key;
        if (def.LabelArgs is { Count: > 0 } args)
        {
            try
            {
                text = string.Format(text, args.ToArray());
            }
            catch (FormatException)
            {
            }
        }
        return text;
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemList.SelectedItem is MenuEntryRow row)
        {
            FillFields(row.Definition);
        }
    }

    private void FillFields(MenuDefinition def)
    {
        SeparatorBox.IsChecked = def.Separator;
        LabelKeyBox.Text = def.LabelKey ?? "";
        IconBox.Text = def.Icon ?? "";
        ActionBox.SelectedItem = def.Action is { } a && MpvPlayerPage.KnownMenuActions.Contains(a) ? a : null;
        CommandBox.Text = def.MpvCommand ?? "";
        EditTitle.Text = def.Separator ? "Separator" : (ResolveLabel(def) ?? "(unlabeled item)");
    }

    private void ItemList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ItemList.SelectedItem is MenuEntryRow { IsSubmenu: true } row && row.Definition.Children is { } children)
        {
            _path.Push((children, row.Definition.LabelKey ?? "submenu"));
            RefreshList();
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_path.Count > 0)
        {
            _path.Pop();
            RefreshList();
        }
    }

    private void CommitFields()
    {
        if (ItemList.SelectedItem is not MenuEntryRow row || row.Definition.Separator)
        {
            return;
        }
        var def = row.Definition;
        def.LabelKey = string.IsNullOrWhiteSpace(LabelKeyBox.Text) ? null : LabelKeyBox.Text.Trim();
        def.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text.Trim();
        def.Action = ActionBox.SelectedItem as string;
        def.MpvCommand = string.IsNullOrWhiteSpace(CommandBox.Text) ? null : CommandBox.Text.Trim();
        if (def.Action is null && def.MpvCommand is null)
        {
            def.LabelKey = def.LabelKey; // keep label, harmless
        }
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        CommitFields();
        CurrentList.Add(new MenuDefinition { LabelKey = "Playlist" });
        RefreshList();
    }

    private void AddSubmenu_Click(object sender, RoutedEventArgs e)
    {
        CommitFields();
        CurrentList.Add(new MenuDefinition { LabelKey = "MenuFile", Children = [] });
        RefreshList();
    }

    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        CommitFields();
        CurrentList.Add(new MenuDefinition { Separator = true });
        RefreshList();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        MoveSelected(-1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        MoveSelected(1);
    }

    private void MoveSelected(int delta)
    {
        CommitFields();
        if (ItemList.SelectedIndex < 0)
        {
            return;
        }
        var index = ItemList.SelectedIndex;
        var target = index + delta;
        if (target < 0 || target >= CurrentList.Count)
        {
            return;
        }
        (CurrentList[target], CurrentList[index]) = (CurrentList[index], CurrentList[target]);
        RefreshList();
        ItemList.SelectedIndex = target;
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedIndex < 0)
        {
            return;
        }
        CurrentList.RemoveAt(ItemList.SelectedIndex);
        RefreshList();
    }

    private void SeparatorBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is MenuEntryRow row)
        {
            row.Definition.Separator = SeparatorBox.IsChecked == true;
            RefreshList();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CommitFields();
            var json = JsonSerializer.Serialize(_root, MenuJsonContext.Default.ListMenuDefinition);
            Directory.CreateDirectory(Path.GetDirectoryName(TargetPath)!);
            File.WriteAllText(TargetPath, json);
            _logger.Info("menu editor saved: {}", TargetPath);

            // Rebuild the live menu bar so changes apply immediately.
            (App.Window as MainWindow)?.RebuildPlayerMenuBar();
            var dialog = new ContentDialog
            {
                Title = "Menu Editor",
                Content = "Saved.",
                CloseButtonText = "OK",
                XamlRoot = RootGrid.XamlRoot,
            };
            _ = dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "menu editor save failed");
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        Reload();
    }
}
