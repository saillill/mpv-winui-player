using System;
using System.Collections.Generic;
using System.Linq;

namespace mpv_winui.Modules.MediaInfo;

/// <summary>
/// One "label : value" pair from a MediaInfo section.
/// </summary>
public sealed class MediaInfoRow
{
    public MediaInfoRow(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }

    /// <summary>UIA name for the row, so a screen reader reads label then value.</summary>
    public string AutomationName => $"{Label}: {Value}";

    /// <summary>Whether the row is a file path (or similar) that benefits from wrapping.</summary>
    public bool IsPathLike =>
        Label.Contains("name", StringComparison.OrdinalIgnoreCase)
        || Label.Contains("file", StringComparison.OrdinalIgnoreCase)
        || Label.Contains("url", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A MediaInfo section (General / Video / Audio / Text / Menu) and its rows.
/// </summary>
public sealed class MediaInfoSection
{
    public MediaInfoSection(string rawName, string displayName, string glyph, IReadOnlyList<MediaInfoRow> rows)
    {
        RawName = rawName;
        DisplayName = displayName;
        Glyph = glyph;
        Rows = rows;
    }

    /// <summary>The name MediaInfo itself printed ("General", "Video", ...).</summary>
    public string RawName { get; }

    /// <summary>The localized caption shown as the card header.</summary>
    public string DisplayName { get; }

    /// <summary>Segoe Fluent Icon glyph for the card header.</summary>
    public string Glyph { get; }

    public IReadOnlyList<MediaInfoRow> Rows { get; }

    public bool HasRows => Rows.Count > 0;

    /// <summary>Row count shown in the header ("3 tracks").</summary>
    public string RowCountText => Rows.Count.ToString();

    /// <summary>Localized caption for this card's copy button.</summary>
    public string CopyLabel { get; init; } = "Copy";

    /// <summary>The section flattened back to "Label: Value" lines for the clipboard.</summary>
    public string ToPlainText() =>
        string.Join(
            Environment.NewLine,
            Rows.Select(r => $"{r.Label}: {r.Value}"));
}

/// <summary>
/// Parses <c>MediaInfo_Inform(handle, 0)</c> output.
///
/// The native library prints a flat text dump: a section name on its own line,
/// then "Label : Value" lines, with one blank line between sections. Raw text
/// in a monospaced box is what made the window look unpolished, so the dump is
/// folded into sections of label/value pairs, which the XAML renders as WinUI
/// cards. Values may contain colons of their own, so only the first one splits.
/// </summary>
public static class MediaInfoReport
{
    public static IReadOnlyList<MediaInfoSection> Parse(string? text, AppLangStrings names)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var sections = new List<MediaInfoSection>();
        string? currentName = null;
        var currentRows = new List<MediaInfoRow>();
        // "Video #1", "Audio #2": MediaInfo prints one section per track, and
        // the index has to survive into the card header or two audio tracks
        // become two identically titled cards.
        var trackIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Flush()
        {
            if (currentName is null)
            {
                return;
            }

            var key = BaseSectionName(currentName);
            trackIndex.TryGetValue(key, out var seen);
            trackIndex[key] = seen + 1;

            var caption = Describe(key, names);
            if (seen > 0)
            {
                caption = $"{caption} #{seen + 1}";
            }

            sections.Add(new MediaInfoSection(currentName, caption, GlyphFor(key), currentRows.ToList())
            {
                CopyLabel = names.Copy,
            });
            currentRows.Clear();
        }

        foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                currentName = null;
                continue;
            }

            // MediaInfo indents values under a label on wrapped lines; those
            // continuations are folded into the previous row rather than
            // becoming a bogus label of their own.
            if (line[0] == ' ' && currentRows.Count > 0 && !line.Contains(':'))
            {
                var last = currentRows[^1];
                currentRows[^1] = new MediaInfoRow(last.Label, $"{last.Value} {line.Trim()}");
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                // A line with no colon starts a section. Anything already
                // buffered belongs to the section that just ended.
                Flush();
                currentName = line.Trim();
                continue;
            }

            // Skip a section-less preamble (MediaInfo sometimes heads with a
            // bare "MediaInfo" line, which the no-colon branch above already
            // treats as a section, hence the guard here is only defensive).
            currentName ??= "General";
            var label = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (label.Length > 0)
            {
                currentRows.Add(new MediaInfoRow(label, value));
            }
        }

        Flush();
        return sections.Where(s => s.HasRows).ToList();
    }

    /// <summary>Strips the "#n" MediaInfo appends to repeated track sections.</summary>
    private static string BaseSectionName(string name)
    {
        var hash = name.IndexOf('#');
        return (hash > 0 ? name[..hash] : name).Trim();
    }

    private static string Describe(string section, AppLangStrings names) => section.ToLowerInvariant() switch
    {
        "general" => names.General,
        "video" => names.Video,
        "audio" => names.Audio,
        "text" => names.Text,
        "menu" => names.Menu,
        _ => names.Other,
    };

    /// <summary>Segoe Fluent Icon glyphs matching the Windows settings language.</summary>
    private static string GlyphFor(string section) => section.ToLowerInvariant() switch
    {
        "general" => "\uE946",   // Info
        "video" => "\uE714",     // Video
        "audio" => "\uE767",     // Volume
        "text" => "\uED1F",      // Subtitles
        "menu" => "\uE8A4",      // Library
        _ => "\uE9D9",           // Diagnostic
    };

    /// <summary>
    /// The localized captions, passed in rather than read from AppContext so
    /// the parser stays pure and testable.
    /// </summary>
    public sealed record AppLangStrings(
        string General,
        string Video,
        string Audio,
        string Text,
        string Menu,
        string Other,
        string Copy)
    {
        public static AppLangStrings FromCurrent()
        {
            var lang = global::mpv_winui.AppContext.AppLang;
            return new AppLangStrings(
                lang.MediaInfoGeneral,
                lang.MediaInfoVideo,
                lang.MediaInfoAudio,
                lang.MediaInfoText,
                lang.MediaInfoMenuSection,
                lang.MediaInfoOther,
                lang.MediaInfoCopyAll);
        }
    }
}
