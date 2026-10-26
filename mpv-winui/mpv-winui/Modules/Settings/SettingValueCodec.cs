using System;
using System.Globalization;

namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// The one place that turns a stored setting value into the typed value the
    /// app asks for.
    ///
    /// <para>**Why this is a separate type.** The conversion used to be copied
    /// into both <see cref="CachedDataSetting"/> and
    /// <see cref="UnpackageAppDataSetting"/>, and both copies carried the same
    /// defect: the bool arm recognised `int` only.</para>
    ///
    /// <para>**The defect, measured.** `EnableDebugLog` was stored as
    /// `REG_DWORD = 1` (in the registry it reads back as `[DWord] 1`).
    /// `ApplicationDataContainer` surfaces a DWORD as an unsigned integral, not
    /// as `int`, so `value is int` was false and `GetValue&lt;bool&gt;` fell
    /// through to the default — `false`. The setting looked enabled in the
    /// registry and in the settings window, and the logger stayed at WARN. Two
    /// days of app logs (2026-10-25, 2026-10-26) contain **zero** `|DEBUG|` and
    /// **zero** `|INFO|` lines while `EnableDebugLog` read `1`. Writing the same
    /// value as the string `"true"` made DEBUG lines appear immediately.</para>
    ///
    /// <para>So the arm now accepts every integral shape a backing store can
    /// return, and <c>mpv-conf-test/SettingValueCodecTests.cs</c> pins each one.
    /// Being pure and dependency-free is what lets that test link it.</para>
    /// </summary>
    public static class SettingValueCodec
    {
        /// <summary>
        /// Converts <paramref name="value"/> to <typeparamref name="T"/>, or
        /// returns <paramref name="defaultValue"/> when the stored value cannot
        /// be expressed as that type.
        ///
        /// <para>Never throws: a read must not fail the app, and it must never
        /// write a default back over the user's stored value.</para>
        /// </summary>
        public static T Coerce<T>(object? value, T defaultValue)
        {
            if (value is null)
            {
                return defaultValue;
            }

            if (value is T typed)
            {
                return typed;
            }

            if (typeof(T) == typeof(bool))
            {
                return (T)(object)ToBool(value, (bool)(object)defaultValue!);
            }

            if (value is string text)
            {
                return CoerceFromString(text, defaultValue);
            }

            return defaultValue;
        }

        /// <summary>
        /// Bool from any integral or textual shape.
        ///
        /// The integral arms are the fix: `ApplicationDataContainer` reports a
        /// REG_DWORD as `uint` (and older imports can leave `long`/`short`/
        /// `byte`), none of which is `int`.
        /// </summary>
        private static bool ToBool(object value, bool defaultValue) => value switch
        {
            bool b => b,

            // Registry DWORD, whichever width the store chose to surface.
            sbyte v => v != 0,
            byte v => v != 0,
            short v => v != 0,
            ushort v => v != 0,
            int v => v != 0,
            uint v => v != 0,
            long v => v != 0,
            ulong v => v != 0,

            // REG_BINARY holding a single byte, which is how some packaged
            // imports land.
            byte[] bytes when bytes.Length > 0 => bytes[0] != 0,

            string s when bool.TryParse(s, out var parsed) => parsed,

            // "1"/"0" written by a hand edit or an older import.
            string s when int.TryParse(s, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var n) => n != 0,

            _ => defaultValue,
        };

        private static T CoerceFromString<T>(string text, T defaultValue)
        {
            if (typeof(T) == typeof(int)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return (T)(object)i;
            }

            if (typeof(T) == typeof(double)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                return (T)(object)d;
            }

            if (typeof(T) == typeof(float)
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                return (T)(object)f;
            }

            if (typeof(T) == typeof(long)
                && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            {
                return (T)(object)l;
            }

            if (typeof(T) == typeof(uint)
                && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u))
            {
                return (T)(object)u;
            }

            if (typeof(T) == typeof(ulong)
                && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ul))
            {
                return (T)(object)ul;
            }

            return defaultValue;
        }
    }
}
