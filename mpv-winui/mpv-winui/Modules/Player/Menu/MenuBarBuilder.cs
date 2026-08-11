using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Language;
using NLog;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace mpv_winui.Modules.Player.Menu;

/// <summary>
/// Builds the WinUI menu bar from <see cref="MenuDefinition"/> entries.
/// Labels are resolved from AppLang (per-language JSON) at build time, so
/// rebuilding after a language change re-localizes the whole bar.
/// </summary>
public static class MenuBarBuilder
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(MenuBarBuilder));

    public static void Build(
        MenuBar menuBar,
        IReadOnlyList<MenuDefinition> menus,
        IReadOnlySet<string> knownActions,
        RoutedEventHandler itemClick)
    {
        menuBar.Items.Clear();
        foreach (var top in menus)
        {
            if (top is null)
            {
                continue;
            }
            if (top.Children is not { Count: > 0 } children)
            {
                continue;
            }

            var barItem = new MenuBarItem
            {
                Title = ResolveLabel(top),
                Tag = top,
                IsTabStop = false,
            };
            AddItems(barItem.Items, children, knownActions, itemClick);
            if (barItem.Items.Count > 0)
            {
                menuBar.Items.Add(barItem);
            }
        }
    }

    private static void AddItems(
        IList<MenuFlyoutItemBase> target,
        IReadOnlyList<MenuDefinition> items,
        IReadOnlySet<string> knownActions,
        RoutedEventHandler itemClick)
    {
        // Drop leading separators, collapse consecutive ones and never leave a
        // dangling divider (trailing, or before a skipped/empty item), so a
        // user override cannot produce malformed menus.
        var pendingSeparator = false;
        foreach (var entry in items)
        {
            if (entry is null)
            {
                continue;
            }
            if (entry.Separator)
            {
                if (target.Count > 0)
                {
                    pendingSeparator = true;
                }
                continue;
            }

            // Resolve what this entry renders to before touching the separator.
            if (entry.Children is { Count: > 0 } children)
            {
                var sub = new MenuFlyoutSubItem
                {
                    Text = ResolveLabel(entry),
                };
                AddItems(sub.Items, children, knownActions, itemClick);
                if (sub.Items.Count == 0)
                {
                    // Empty submenu: drop it and any pending separator.
                    pendingSeparator = false;
                    continue;
                }

                if (pendingSeparator)
                {
                    target.Add(new MenuFlyoutSeparator());
                    pendingSeparator = false;
                }
                target.Add(sub);
                continue;
            }

            if (!string.IsNullOrEmpty(entry.Action) && !knownActions.Contains(entry.Action))
            {
                _logger.Warn("menu item skipped, unknown action={}, id={}", entry.Action, entry.Id);
                continue;
            }
            if (string.IsNullOrEmpty(entry.Action) && string.IsNullOrEmpty(entry.MpvCommand))
            {
                _logger.Warn("menu item skipped, no action or command, id={}", entry.Id);
                continue;
            }

            if (pendingSeparator)
            {
                target.Add(new MenuFlyoutSeparator());
                pendingSeparator = false;
            }

            var item = new MenuFlyoutItem
            {
                Text = ResolveLabel(entry),
                Tag = entry,
                IsTabStop = false,
            };
            if (!string.IsNullOrEmpty(entry.Icon))
            {
                item.Icon = new FontIcon
                {
                    Glyph = entry.Icon,
                    FontFamily = new FontFamily("ms-appx:///Assets/FluentSystemIcons-Regular.ttf#FluentSystemIcons-Regular"),
                };
            }
            item.Click += itemClick;
            target.Add(item);
        }
    }

    private static string ResolveLabel(MenuDefinition def)
    {
        var text = def.LabelKey is { } key ? ResolveAppLang(key) ?? key : string.Empty;
        if (def.LabelArgs is { Count: > 0 } args)
        {
            try
            {
                text = string.Format(text, args.ToArray());
            }
            catch (FormatException)
            {
                // Keep the raw label when the format string is invalid.
            }
        }
        return text;
    }

    private static string? ResolveAppLang(string key)
    {
        // AppLang is annotated with DynamicallyAccessedMembers(PublicProperties),
        // so the properties are preserved for trimming/AOT.
        var prop = typeof(AppLang).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(AppContext.AppLang) as string;
    }
}
