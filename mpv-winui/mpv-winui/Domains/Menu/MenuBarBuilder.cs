using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using mpv_winui.Lexicon.Language;
using NLog;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace mpv_winui.Domains.Menu;

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
        RoutedEventHandler itemClick,
        Func<string?, string?>? commandHintResolver = null)
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
            AddItems(barItem.Items, children, knownActions, itemClick, commandHintResolver);
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
        RoutedEventHandler itemClick,
        Func<string?, string?>? commandHintResolver)
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
                AddItems(sub.Items, children, knownActions, itemClick, commandHintResolver);
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

            var text = ResolveLabel(entry);
            // Display-only shortcut hint from input.conf (never a real
            // KeyboardAccelerator: keys are forwarded to mpv by a hook, an
            // accelerator would fire the command twice).
            if (commandHintResolver is not null
                && !string.IsNullOrEmpty(entry.MpvCommand)
                && commandHintResolver(entry.MpvCommand) is { } hint)
            {
                text = $"{text}  ({hint})";
            }

            var item = new MenuFlyoutItem
            {
                Text = text,
                Tag = entry,
                IsTabStop = false,
            };
            if (!string.IsNullOrEmpty(entry.Icon))
            {
                item.Icon = new FontIcon
                {
                    Glyph = entry.Icon,
                    FontFamily = new FontFamily(IconFonts.FluentSystemIconsUri),
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
        if (prop is null)
        {
            // The caller falls back to the raw key, so the menu would render
            // "MenuFile" where the user expects "文件". A typo in menus.json
            // is the only way here, and it is invisible in the UI - the label
            // looks deliberate, just wrong - so it has to be said in the log.
            _logger.Warn("menu labelKey has no AppLang property, id will show raw: {0}", key);
            return null;
        }

        return prop.GetValue(AppContext.AppLang) as string;
    }
}
