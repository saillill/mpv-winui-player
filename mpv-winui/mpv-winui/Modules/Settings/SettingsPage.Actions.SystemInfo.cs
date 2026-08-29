using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using mpv_winui.Modules.Activation;
using mpv_winui.Modules.Common.Utils;
using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Language;
using mpv_winui.Modules.Player;
using mpv_winui.Modules.Settings.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Windows.Storage.Pickers;

namespace mpv_winui.Modules.Settings;

public sealed partial class SettingsPage
{
    private static List<OptionChoice> BuildAudioDeviceChoices()
    {
        lock (DeviceChoicesLock)
        {
            if (_audioDeviceChoicesCache is not null)
            {
                return _audioDeviceChoicesCache;
            }
        }

        var choices = new List<OptionChoice>
        {
            new("auto", AppContext.AppLang.OptionValueAuto),
        };

        var enumerated = false;
        try
        {
            var devices = AppContext.GetAudioDevices?.Invoke();
            if (devices is not null)
            {
                enumerated = true;
                foreach (var device in devices)
                {
                    var label = string.IsNullOrWhiteSpace(device.Description) ? device.Name : device.Description;
                    choices.Add(new OptionChoice(device.Name, label));
                }
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Warn(ex, "Failed to enumerate audio devices");
        }

        // Only cache when the player's audio-device source was available; a
        // premature cache would pin the auto-only list forever.
        if (enumerated)
        {
            lock (DeviceChoicesLock)
            {
                _audioDeviceChoicesCache = choices;
            }
        }
        return choices;
    }

    private static readonly object DeviceChoicesLock = new();
    private static List<OptionChoice>? _audioDeviceChoicesCache;
    private static List<OptionChoice>? _gpuChoicesCache;

    /// <summary>
    /// Warms the cached device-choice lists on a background thread so opening
    /// the settings page never blocks the UI thread on WMI/native enumeration
    /// (audit A2). The synchronous providers still fall back to a first-call
    /// enumeration when the warm-up has not finished.
    /// </summary>
    internal static void WarmDeviceChoices()
    {
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                _ = BuildGpuAdapterChoices();
                _ = BuildAudioDeviceChoices();
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Warn(ex, "Device choice warm-up failed");
            }
        });
    }

    /// <summary>Lists installed display adapters (DXGI descriptions) for d3d11-adapter.</summary>
    private static List<OptionChoice> BuildGpuAdapterChoices()
    {
        lock (DeviceChoicesLock)
        {
            if (_gpuChoicesCache is not null)
            {
                return _gpuChoicesCache;
            }
        }

        var choices = new List<OptionChoice>
        {
            new("", AppContext.AppLang.OptionValueAuto),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumerated = false;

        // DXGI is the official, fast enumeration path (audit A2); the player
        // page provides it through the native component. WMI remains the
        // fallback for a settings session without an initialized player.
        var dxgiAdapters = AppContext.GetGpuAdapters?.Invoke();
        if (dxgiAdapters is not null)
        {
            enumerated = true;
            foreach (var adapter in dxgiAdapters)
            {
                var name = string.IsNullOrWhiteSpace(adapter.Description)
                    ? adapter.Name
                    : adapter.Description;
                if (!string.IsNullOrWhiteSpace(name)
                    && !IsVirtualDisplayAdapter(name)
                    && seen.Add(name))
                {
                    choices.Add(new OptionChoice(name, name));
                }
            }
        }
        else
        {
            try
            {
                // The display-class registry lists every registered adapter,
                // including disabled/headless cards. Only adapters currently
                // driving a display (non-zero current resolution) are usable
                // for d3d11 presentation.
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name, CurrentHorizontalResolution FROM Win32_VideoController");
                using var results = searcher.Get();
                foreach (System.Management.ManagementObject obj in results)
                {
                    using (obj)
                    {
                        if (obj["Name"] is string name
                            && !string.IsNullOrWhiteSpace(name)
                            && obj["CurrentHorizontalResolution"] is uint resolution
                            && resolution > 0
                            && !IsVirtualDisplayAdapter(name)
                            && seen.Add(name))
                        {
                            choices.Add(new OptionChoice(name, name));
                        }
                    }
                }
                enumerated = true;
            }
            catch (Exception ex)
            {
                AppContext.AppLogger.Error(ex, "Failed to enumerate display adapters");
            }
        }

        // Only cache when a source was available; a premature cache would pin
        // the auto-only list before the player starts.
        if (!enumerated)
        {
            return choices;
        }

        lock (DeviceChoicesLock)
        {
            _gpuChoicesCache = choices;
        }
        return choices;
    }

    /// <summary>Skips software/remote display adapters that are not real GPUs.</summary>
    private static bool IsVirtualDisplayAdapter(string description) =>
        description.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Remote Display", StringComparison.OrdinalIgnoreCase)
        || description.Contains("基本显示", StringComparison.Ordinal)
        || description.Contains("远程显示", StringComparison.Ordinal);
}
