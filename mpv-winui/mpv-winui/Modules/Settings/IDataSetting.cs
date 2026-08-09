using System.Collections.Generic;

namespace mpv_winui.Modules.Settings
{
    public interface IDataSetting
    {
        T GetValue<T>(string propertyName, T defaultValue);
        bool SetValue<T>(string propertyName, T value);
        void ResetAll();
        void ResetKeys(IEnumerable<string> keys);
        IReadOnlyDictionary<string, object> ExportAll();
        void ImportAll(IReadOnlyDictionary<string, object> values);
    }
}
