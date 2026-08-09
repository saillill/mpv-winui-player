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
