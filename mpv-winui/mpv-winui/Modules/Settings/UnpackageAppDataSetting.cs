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
            //HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\ikas-mc\mpvw\app
            var application = ApplicationData.GetForUnpackaged(AppData.AppDataPublisher, AppData.AppDataId);
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
                // One converter for every backend; see SettingValueCodec for the
                // defect this replaced (a REG_DWORD bool read back as false, so
                // EnableDebugLog never took effect while it looked enabled).
                return SettingValueCodec.Coerce(_container.Values[propertyName], defaultValue);
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
                // Imported bools from packaged builds arrive as raw REG_NONE
                // values that ApplicationData cannot read back reliably; store
                // them as "true"/"false" like SetValue does.
                _container.Values[item.Key] = item.Value is bool b ? (b ? "true" : "false") : item.Value;
            }
        }
    }
}
