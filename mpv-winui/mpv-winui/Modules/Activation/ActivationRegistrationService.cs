using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.Activation
{
    /// <summary>
    /// File/protocol association through the official WindowsAppSDK API
    /// (ActivationRegistrationManager) instead of hand-written registry keys.
    /// Only meaningful for unpackaged builds; packaged apps declare
    /// associations in Package.appxmanifest.
    /// </summary>
    public sealed class ActivationRegistrationService
    {
        private static readonly Lazy<ActivationRegistrationService> _lazyValue = new(() => new ActivationRegistrationService(), true);

        public static ActivationRegistrationService Instance => _lazyValue.Value;

        private const string AssociationDisplayName = "mpv-winui";
        private static string ExePath => Environment.ProcessPath ?? string.Empty;
        private static string LogoPath => $"{ExePath},0";

        private ActivationRegistrationService()
        {
        }

        /// <summary>
        /// Matches the app-id hash used by WindowsAppSDK's association
        /// registration (see Association.cpp).
        /// </summary>
        public static string ComputeAppId(string? exePath = null)
        {
            var seed = (exePath ?? Environment.ProcessPath ?? string.Empty).ToLowerInvariant();

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            foreach (var ch in seed)
            {
                hash = (hash ^ ((byte)(ch & 0xFF))) * prime;
                hash = (hash ^ ((byte)(ch >> 8))) * prime;
            }

            return "App." + hash.ToString("x");
        }

        public Task RegisterAsync(IReadOnlyList<string> extensions)
        {
            if (extensions is null || extensions.Count == 0)
            {
                return Task.CompletedTask;
            }

            var fileTypes = extensions.ToArray();
            return Task.Run(() =>
            {
                try
                {
                    ActivationRegistrationManager.RegisterForFileTypeActivation(
                        fileTypes, LogoPath, AssociationDisplayName, ["open"], ExePath);
                }
                catch (Exception ex)
                {
                    AppContext.AppLogger.Error(ex, "RegisterForFileTypeActivation failed");
                    throw;
                }

                EnsureShellCommand(ComputeAppId() + ".File");
            });
        }

        public Task UnregisterAsync(IReadOnlyList<string> extensions)
        {
            if (extensions is null || extensions.Count == 0)
            {
                return Task.CompletedTask;
            }

            var fileTypes = extensions.ToArray();
            return Task.Run(() =>
            {
                try
                {
                    ActivationRegistrationManager.UnregisterForFileTypeActivation(fileTypes, null);
                }
                catch (Exception)
                {
                    // An extension may already be unregistered; keep going.
                }
            });
        }

        public Task<IReadOnlyList<string>> GetRegisteredExtensionsAsync()
        {
            return Task.Run(GetRegisteredExtensions);
        }

        public Task RegisterProtocolAsync(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
            {
                return Task.CompletedTask;
            }

            var s = NormalizeScheme(scheme);
            return Task.Run(() =>
            {
                try
                {
                    ActivationRegistrationManager.RegisterForProtocolActivation(s, LogoPath, AssociationDisplayName, ExePath);
                }
                catch (Exception ex)
                {
                    AppContext.AppLogger.Error(ex, "RegisterForProtocolActivation failed");
                    throw;
                }

                EnsureShellCommand(ComputeAppId() + ".Protocol");
            });
        }

        public Task UnregisterProtocolAsync(string scheme)
        {
            if (string.IsNullOrWhiteSpace(scheme))
            {
                return Task.CompletedTask;
            }

            var s = NormalizeScheme(scheme);
            return Task.Run(() =>
            {
                try
                {
                    ActivationRegistrationManager.UnregisterForProtocolActivation(s, null);
                }
                catch (Exception)
                {
                    // The scheme may already be unregistered; keep going.
                }
            });
        }

        public Task<IReadOnlyList<string>> GetRegisteredProtocolsAsync()
        {
            return Task.Run(GetRegisteredProtocols);
        }

        private static string NormalizeScheme(string scheme)
        {
            var s = scheme.Trim();
            var index = s.IndexOf("://", StringComparison.Ordinal);
            if (index >= 0)
            {
                s = s[..index];
            }

            return s.TrimEnd(':').ToLowerInvariant();
        }

        private static IReadOnlyList<string> GetRegisteredExtensions()
        {
            var appId = ComputeAppId();
            var progId = appId + ".File";
            var path = $@"Software\Microsoft\WindowsAppRuntimeApplications\{appId}\Capabilties\FileAssociations";

            using var key = Registry.CurrentUser.OpenSubKey(path);
            if (key is null)
            {
                return [];
            }

            var result = new List<string>();
            foreach (var name in key.GetValueNames())
            {
                if (name.StartsWith('.') && string.Equals(key.GetValue(name) as string, progId, StringComparison.Ordinal))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static IReadOnlyList<string> GetRegisteredProtocols()
        {
            var appId = ComputeAppId();
            var progId = appId + ".Protocol";
            var path = $@"Software\Microsoft\WindowsAppRuntimeApplications\{appId}\Capabilties\UrlAssociations";

            using var key = Registry.CurrentUser.OpenSubKey(path);
            if (key is null)
            {
                return [];
            }

            var result = new List<string>();
            foreach (var name in key.GetValueNames())
            {
                if (string.Equals(key.GetValue(name) as string, progId, StringComparison.Ordinal))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        /// <summary>
        /// Makes sure HKCU\Software\Classes\&lt;progId&gt;\shell\open\command has a
        /// launch command. WindowsAppSDK's RegisterForFileTypeActivation /
        /// RegisterForProtocolActivation create the association entry under
        /// ...\WindowsAppRuntimeApplications but on some unpackaged setups the
        /// ProgId's shell verb command is left empty, so Windows cannot start
        /// the app from a file/protocol. We repair that here.
        /// </summary>
        private static void EnsureShellCommand(string progId)
        {
            try
            {
                var command = "\"" + ExePath + "\" \"%1\"";
                var commandKeyPath = $@"Software\Classes\{progId}\shell\open\command";

                var existing = Registry.GetValue($@"HKEY_CURRENT_USER\{commandKeyPath}", null, null) as string;
                if (string.IsNullOrWhiteSpace(existing))
                {
                    using var commandKey = Registry.CurrentUser.CreateSubKey(commandKeyPath);
                    commandKey?.SetValue(null, command);
                }

                var defaultIconKeyPath = $@"Software\Classes\{progId}\DefaultIcon";
                var defaultIcon = Registry.GetValue($@"HKEY_CURRENT_USER\{defaultIconKeyPath}", null, null) as string;
                if (string.IsNullOrWhiteSpace(defaultIcon))
                {
                    using var defaultIconKey = Registry.CurrentUser.CreateSubKey(defaultIconKeyPath);
                    defaultIconKey?.SetValue(null, LogoPath);
                }

                var applicationKeyPath = $@"Software\Classes\{progId}\Application";
                var applicationName = Registry.GetValue($@"HKEY_CURRENT_USER\{applicationKeyPath}", "ApplicationName", null) as string;
                if (string.IsNullOrWhiteSpace(applicationName))
                {
                    using var applicationKey = Registry.CurrentUser.CreateSubKey(applicationKeyPath);
                    applicationKey?.SetValue("ApplicationName", AssociationDisplayName);
                }
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "EnsureShellCommand failed for " + progId);
            }
        }

        /// <summary>
        /// True when the ProgId Windows would actually launch for this app has a
        /// usable open command. A registration can exist under
        /// ...\WindowsAppRuntimeApplications while the HKCU\Software\Classes ProgId
        /// has an empty or missing shell\open\command, in which case Windows shows
        /// the app but cannot start it from a file.
        /// </summary>
        public static bool HasUsableShellCommand()
        {
            try
            {
                var path = $@"Software\Classes\{ComputeAppId()}.File\shell\open\command";
                var value = Registry.GetValue($@"HKEY_CURRENT_USER\{path}", null, null) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "shell command probe failed");
                return false;
            }
        }
    }
}
