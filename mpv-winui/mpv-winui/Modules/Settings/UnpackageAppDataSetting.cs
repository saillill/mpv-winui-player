using Microsoft.Windows.Storage;
using mpv_winui.Modules.FileSystem;
using System;
using System.Collections.Generic;

namespace mpv_winui.Modules.Settings
{
    public class UnpackageAppDataSetting : IDataSetting
    {
        private readonly ApplicationDataContainer _container;

        public UnpackageAppDataSetting(string typeName)
        {
            //HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app
            var application = ApplicationData.GetForUnpackaged(AppData.AppDataId, AppData.AppDataId);
            _container = application.LocalSettings.CreateContainer(typeName, ApplicationDataCreateDisposition.Always);
        }

        public T GetValue<T>(string propertyName, T defaultValue)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return defaultValue;
            }

            try
            {
                var value = _container.Values[propertyName];
                if (value is T t)
                {
                    return t;
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
                    // Imported config files store every value as a string; the
                    // registry then holds REG_SZ instead of the original type,
                    // so numeric properties need string -> number conversion.
                    // InvariantCulture keeps round-trips stable across regions
                    // (export/import must not depend on the user's number format).
                    if (typeof(T) == typeof(int) && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
                    {
                        return (T)(object)i;
                    }
                    if (typeof(T) == typeof(double) && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    {
                        return (T)(object)d;
                    }
                    if (typeof(T) == typeof(float) && float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    {
                        return (T)(object)f;
                    }
                    if (typeof(T) == typeof(long) && long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l))
                    {
                        return (T)(object)l;
                    }
                    if (typeof(T) == typeof(uint) && uint.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var u))
                    {
                        return (T)(object)u;
                    }
                    if (typeof(T) == typeof(ulong) && ulong.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ul))
                    {
                        return (T)(object)ul;
                    }
                }
            }
            catch (System.Exception)
            {
                // A read must never write: falling back to the default is
                // enough, and writing here could clobber user data or the
                // migration marker with a default value on transient errors.
            }

            return defaultValue;
        }

        public bool SetValue<T>(string propertyName, T value)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            // Unpackaged ApplicationData stores booleans as REG_NONE, which cannot be
            // read back reliably by ApplicationDataContainer. Store them as "true"/"false".
            if (typeof(T) == typeof(bool))
            {
                _container.Values[propertyName] = (bool)(object)value! ? "true" : "false";
                return true;
            }

            _container.Values[propertyName] = value;

            return true;
        }

        public void ResetAll()
        {
            _container.Values.Clear();
        }

        public void ResetKeys(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    _container.Values.Remove(key);
                }
            }
        }

        public IReadOnlyDictionary<string, object> ExportAll()
        {
            var values = new Dictionary<string, object>();
            foreach (var item in _container.Values)
            {
                values[item.Key] = item.Value;
            }
            return values;
        }

        public void ImportAll(IReadOnlyDictionary<string, object> values)
        {
            _container.Values.Clear();
            foreach (var item in values)
            {
                _container.Values[item.Key] = item.Value;
            }
        }
    }
}
