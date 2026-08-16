using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace mpv_winui.Modules.Settings
{
    /// <summary>
    /// Memory-first settings backend. Reads hit a snapshot taken once at
    /// construction; writes update memory immediately and flush to the
    /// underlying backend after a short debounce, so high-frequency writes
    /// (volume, PiP rect, theme sliders) never touch the registry or
    /// ApplicationData on every event, and ApplyAll reads the snapshot
    /// instead of hundreds of registry round-trips.
    /// </summary>
    public sealed class CachedDataSetting : IDataSetting, IDisposable
    {
        private const int FlushDebounceMilliseconds = 300;

        private readonly IDataSetting _inner;
        private readonly object _gate = new();
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
        private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
        private readonly Timer _flushTimer;
        private bool _disposed;

        public CachedDataSetting(IDataSetting inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            foreach (var pair in inner.ExportAll())
            {
                _values[pair.Key] = pair.Value;
            }
            _flushTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public T GetValue<T>(string propertyName, T defaultValue)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return defaultValue;
            }

            lock (_gate)
            {
                if (!_values.TryGetValue(propertyName, out var value) || value is null)
                {
                    return defaultValue;
                }
                if (value is T typed)
                {
                    return typed;
                }
                if (typeof(T) == typeof(bool))
                {
                    if (value is string s && bool.TryParse(s, out var b))
                    {
                        return (T)(object)b;
                    }
                    if (value is int i)
                    {
                        return (T)(object)(i != 0);
                    }
                    if (value is byte[] bytes && bytes.Length > 0)
                    {
                        return (T)(object)(bytes[0] != 0);
                    }
                }
                if (value is string text)
                {
                    if (typeof(T) == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    {
                        return (T)(object)i;
                    }
                    if (typeof(T) == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    {
                        return (T)(object)d;
                    }
                    if (typeof(T) == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    {
                        return (T)(object)f;
                    }
                    if (typeof(T) == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    {
                        return (T)(object)l;
                    }
                    if (typeof(T) == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u))
                    {
                        return (T)(object)u;
                    }
                    if (typeof(T) == typeof(ulong) && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ul))
                    {
                        return (T)(object)ul;
                    }
                }
            }

            return defaultValue;
        }

        public bool SetValue<T>(string propertyName, T value)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            lock (_gate)
            {
                _values[propertyName] = value!;
                _dirty.Add(propertyName);
                _flushTimer.Change(FlushDebounceMilliseconds, Timeout.Infinite);
            }
            return true;
        }

        public void ResetAll()
        {
            lock (_gate)
            {
                _values.Clear();
                _dirty.Clear();
                _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _inner.ResetAll();
            }
        }

        public void ResetKeys(IEnumerable<string> keys)
        {
            lock (_gate)
            {
                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    _values.Remove(key);
                    _dirty.Add(key);
                }
                _flushTimer.Change(FlushDebounceMilliseconds, Timeout.Infinite);
            }
        }

        public IReadOnlyDictionary<string, object> ExportAll()
        {
            lock (_gate)
            {
                return new Dictionary<string, object>(_values);
            }
        }

        public void ImportAll(IReadOnlyDictionary<string, object> values)
        {
            lock (_gate)
            {
                _values.Clear();
                if (values is not null)
                {
                    foreach (var pair in values)
                    {
                        _values[pair.Key] = pair.Value;
                    }
                }
                _dirty.Clear();
                _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _inner.ImportAll(values!);
            }
        }

        /// <summary>Writes all dirty keys to the underlying backend now.</summary>
        public void Flush()
        {
            string[] dirty;
            lock (_gate)
            {
                if (_dirty.Count == 0)
                {
                    return;
                }
                dirty = new string[_dirty.Count];
                _dirty.CopyTo(dirty);
                _dirty.Clear();
                _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            foreach (var key in dirty)
            {
                try
                {
                    object? value;
                    bool hasValue;
                    lock (_gate)
                    {
                        hasValue = _values.TryGetValue(key, out value);
                    }
                    if (hasValue)
                    {
                        _inner.SetValue(key, value!);
                    }
                    else
                    {
                        _inner.ResetKeys((string[])[key]);
                    }
                }
                catch
                {
                    // A transient backend failure must not drop the write;
                    // mark it dirty again so the next flush retries.
                    lock (_gate)
                    {
                        if (!_disposed)
                        {
                            _dirty.Add(key);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _flushTimer.Dispose();
            Flush();
        }
    }
}
