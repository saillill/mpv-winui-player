using System.Collections.Generic;
using Windows.Storage;

namespace mpv_winui.Modules.Settings
{
    public class AppDataSetting : IDataSetting
    {
        private readonly ApplicationDataContainer _container;

        public AppDataSetting(string typeName)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            _container = localSettings.CreateContainer(typeName, ApplicationDataCreateDisposition.Always);
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
