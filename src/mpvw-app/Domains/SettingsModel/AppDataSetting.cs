using System.Collections.Generic;
using Microsoft.Windows.Storage;

namespace mpv_winui.Domains.SettingsModel
{
    /// <summary>
    /// Settings backend for the PACKAGED build.
    ///
    /// Same store as UnpackageAppDataSetting, reached from the other side: packaged
    /// builds use ApplicationData.GetDefault(), unpackaged ones
    /// ApplicationData.GetForUnpackaged(publisher, product). Both are the Windows App
    /// SDK surface -- this file was the last one still on Windows.Storage
    /// (ApplicationData.Current), which the WinUI analyzer flags as WUI1001.
    ///
    /// The two APIs are not spelling variants: the old Current only answers inside a
    /// package, and the replacement makes that explicit by requiring either
    /// GetDefault (packaged) or an identity (unpackaged). That is why this is a
    /// one-line change here and a larger one in UnpackageAppDataSetting, which was
    /// already migrated when the app went self-contained.
    ///
    /// Not exercised by the running app on this machine: AppSettings chooses this
    /// backend only when PackageHelper.IsPackaged is true, and the deployed build is
    /// unpackaged. It is kept correct rather than dead, which is the point of
    /// migrating it at all.
    /// </summary>
    public class AppDataSetting : IDataSetting
    {
        private readonly ApplicationDataContainer _container;

        public AppDataSetting(string typeName)
        {
            var application = ApplicationData.GetDefault();
            _container = application.LocalSettings.CreateContainer(
                typeName, ApplicationDataCreateDisposition.Always);
        }

        public T GetValue<T>(string propertyName, T defaultValue)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return defaultValue;
            }

            var value = _container.Values[propertyName];
            if (value is T t)
            {
                return t;
            }

            // Imported config files store every value as a string (and the
            // unpackaged backend persists bools as "true"/"false"); mirror the
            // string -> T conversion so both backends round-trip identically.
            if (value is string text)
            {
                if (typeof(T) == typeof(bool) && bool.TryParse(text, out var b))
                {
                    return (T)(object)b;
                }
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
                if (typeof(T) == typeof(ulong) && ulong.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ul))
                {
                    return (T)(object)ul;
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
