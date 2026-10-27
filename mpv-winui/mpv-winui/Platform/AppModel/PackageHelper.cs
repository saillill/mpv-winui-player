using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
namespace mpv_winui.Platform.AppModel
{
    public class PackageHelper
    {
        private static readonly Lazy<bool> _isPackagedValue = new(CheckPackaged, true);

        public static readonly bool IsPackaged = _isPackagedValue.Value;

        private static readonly Lazy<string> _appNameValue = new(GetAppName, true);

        public static readonly string AppName = _appNameValue.Value;

        private static readonly Lazy<string> _appVersionValue = new(GetAppVersion, true);

        public static readonly string AppVersion = _appVersionValue.Value;

        public static bool CheckPackaged()
        {
            try
            {
                return Package.Current != null && Package.Current.Id != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetAppVersion()
        {
            if (IsPackaged)
            {
                return $"{Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}";
            }
            else
            {
                return AppContext.AppLang.AppVersion;
            }
        }

        public static string GetAppName()
        {
            if (IsPackaged)
            {
                // Package.GetAppListEntries() is a Windows 10 2004 (19041) API,
                // but this app advertises support from 1809 (17763) and the
                // package manifest still does. Calling it unconditionally would
                // throw on 1809 through 19039 for a value that is only a nicer
                // spelling of DisplayName, so it is guarded and the fallback is
                // the plain name. The guard is written with
                // OperatingSystem.IsWindowsVersionAtLeast because the platform
                // compatibility analyzer recognises exactly this form: without
                // it CA1416 reports the call site, which is how this was found.
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                {
                    if (Package.Current.GetAppListEntries().FirstOrDefault() is AppListEntry entry)
                    {
                        return entry.DisplayInfo.DisplayName;
                    }
                }
                return Package.Current.DisplayName;
            }
            else
            {
                return AppContext.AppLang.AppName;
            }
        }
    }
}
