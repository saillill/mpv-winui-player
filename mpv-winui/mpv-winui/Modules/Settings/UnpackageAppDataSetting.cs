using Microsoft.Windows.Storage;
using mpv_winui.Modules.FileSystem;

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
            }
            catch (System.Exception)
            {
                //error when no key 
                _container.Values[propertyName] = defaultValue;
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
    }
}
