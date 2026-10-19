using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace mpv_winui.Modules.Settings;

/// <summary>
/// The escaping grammar shared by the two places a setting's value is written:
/// mpv's command-string parser (<c>set option "value"</c>) and mpv.conf
/// (<c>option="value"</c>). Both accept C-style escapes inside quotes, so a
/// Windows path has to have its backslashes doubled or mpv silently keeps the
/// previous value.
///
/// <para>This lives in its own file, with no dependency on <c>AppContext</c> or
/// on WinUI, so <c>mpv-conf-test</c> can link it and assert the round trip
/// directly. That matters more than it sounds: the two functions are inverses,
/// and the failure mode of getting that wrong is not a wrong value but an
/// unbounded one.</para>
///
/// <para><b>Why this is a separate type at all.</b> The pair used to be a single
/// private <c>Q()</c> in <c>MpvSettings</c> with no inverse. The write side
/// escaped, the read side stored the escaped text verbatim, and the next write
/// escaped it again — the backslash count doubling on every launch. Measured
/// 2026-09-21: a single <c>audio-device</c> value grew
/// <c>%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf</c> to <b>896 MB with 884
/// newlines</b>, and the read path started throwing
/// <c>System.OutOfMemoryException</c> on every startup. A test asserting
/// <c>Unquote(Quote(v)) == v</c> would have failed on the commit that
/// introduced the asymmetry, which is why the pair is now reachable from a
/// test.</para>
/// </summary>
internal static class MpvConfValueCodec
{
    /// <summary>
    /// Quote and escape a value for mpv's command-string or config grammar.
    /// </summary>
    internal static string Quote(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    /// <summary>
    /// Undo exactly one round of <see cref="Quote"/>.
    ///
    /// <para>Quotes are stripped only when they are the outermost pair, so an
    /// unquoted value passes through untouched. The decoder is deliberately not
    /// recursive: one call undoes one round, which is what keeps
    /// <c>Unquote(Quote(v)) == v</c> true for every input, including values that
    /// themselves contain quotes and backslashes.</para>
    ///
    /// <para>A file already damaged by the missing-inverse bug cannot be fully
    /// restored by one pass — its escape nesting is no longer well-formed — so
    /// this repairs future writes, not existing damage.</para>
    /// </summary>
    internal static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                if (next == '\\' || next == '"')
                {
                    sb.Append(next);
                    i++;
                    continue;
                }
            }

            sb.Append(value[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// The mpv-side spellings a handful of settings encode a special .NET value
    /// as. Each one has to be decoded symmetrically with <c>MpvSettings.ToCommand</c>,
    /// which is where the encodings live.
    ///
    /// <para>These exist because the encoder does not emit the property's value
    /// verbatim: it emits the <em>mpv</em> syntax for it. <c>ToCommand</c> turns
    /// <c>AbLoopA == 0</c> into <c>ab-loop-a=no</c>, <c>AbLoopCount == -1</c> into
    /// <c>ab-loop-count=inf</c>, <c>LoopFile == true</c> into <c>loop-file=inf</c>,
    /// and appends an <c>MiB</c> unit to the demuxer limits. A reader that only
    /// knew <c>double.TryParse</c> could not read back a single one of them.</para>
    ///
    /// <para>Measured before this existed (2026-09-21 log, 60 occurrences across
    /// 6 settings): the read path warned <c>mpv.conf value not convertible</c> and
    /// left the stored setting alone, so a value the user had set — and that mpv
    /// was genuinely running with — reverted in the UI on the next launch.</para>
    /// </summary>
    private static readonly Dictionary<string, double> NumericSentinels = new(StringComparer.OrdinalIgnoreCase)
    {
        // ab-loop-a / ab-loop-b: `no` is mpv's "no loop point", which the app
        // stores as 0 (ToCommand: value > 0 ? seconds : "no").
        ["no"] = 0d,
        // ab-loop-count: `inf` is "loop forever", which the app stores as -1
        // (ToCommand: value < 0 ? "inf" : count). The row's range is -1..9999,
        // so -1 is the app's own sentinel, not an invented one.
        ["inf"] = -1d,
    };

    /// <summary>
    /// Parse an mpv.conf value into the .NET value the property stores, undoing
    /// the encoding <c>MpvSettings.ToCommand</c> applied.
    ///
    /// <para>The awkward part is that the right reading depends on the property's
    /// type, because the same text means different things: <c>no</c> is a loop
    /// point for a <see cref="double"/> and false for a <see cref="bool"/>, and
    /// <c>inf</c> is -1 for an <see cref="int"/> and true for a <see cref="bool"/>.
    /// So the type is a parameter rather than something guessed from the text.</para>
    ///
    /// <para>A leading <c>+</c> is mpv's "add to the current value" prefix, not
    /// part of the number; it is stripped so a hand-written
    /// <c>volume=+5</c> does not become an unreadable value.</para>
    /// </summary>
    /// <returns>The value, or null when the text cannot be represented.</returns>
    internal static object? ParseMpvValue(Type type, string text)
    {
        var raw = text.Trim();
        if (raw.StartsWith('+'))
        {
            raw = raw[1..].TrimStart();
        }

        if (type == typeof(string))
        {
            return Unquote(raw);
        }

        if (type == typeof(bool))
        {
            return raw.ToLowerInvariant() switch
            {
                "yes" or "true" or "inf" => true,
                "no" or "false" => false,
                _ => null,
            };
        }

        // A hand-edited value may carry a unit the encoder does not use. Anything
        // other than MiB is refused rather than converted: rounding a size the
        // user wrote exactly is worse than leaving the stored setting alone.
        if (type == typeof(int) || type == typeof(long))
        {
            if (raw.EndsWith("MiB", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw[..^3].TrimEnd();
            }

            if (NumericSentinels.TryGetValue(raw, out var sentinel))
            {
                return (int)sentinel;
            }

            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            {
                return null;
            }

            return type == typeof(int) ? (int)l : l;
        }

        if (type == typeof(double) || type == typeof(float))
        {
            if (NumericSentinels.TryGetValue(raw, out var sentinel))
            {
                return sentinel;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                return null;
            }

            return type == typeof(float) ? (float)d : d;
        }

        return null;
    }
}
