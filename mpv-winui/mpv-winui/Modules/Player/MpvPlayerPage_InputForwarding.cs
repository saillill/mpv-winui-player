using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using mpv;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static Windows.Win32.PInvoke;

namespace mpv_winui.Modules.Player
{
    /// <summary>
    /// Input forwarding for embedded composition mode: libmpv never sees
    /// Win32 keyboard or mouse input here, so a thread-level keyboard hook,
    /// window-activation key flush and WinUI pointer events translate user
    /// input into mpv keydown/keyup commands (VK -> mpv key-code port in
    /// Modules/Mpv).
    /// </summary>
    public sealed partial class MpvPlayerPage
    {

        private HHOOK? _hHook;
        private static bool _suppressKeyboard = false;

        private unsafe void SetupKeyboardInput()
        {
            _hHook = SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD, &MessageHookProc, HINSTANCE.Null, GetCurrentThreadId());

            App.Window?.Activated += Window_Activated;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                SendAllKeyUp();
            }
        }

        private void CleanupKeyboardInput()
        {
            App.Window?.Activated -= Window_Activated;

            if (_hHook is { IsNull: false } hHook)
            {
                UnhookWindowsHookEx(hHook);
            }
            _hHook = null;
            // Note: do not null the shared _selfWeakReference here - it is also
            // used by the window subclass (MpvPlayerPage_Display) and mouse
            // forwarding (MpvPlayerPage_Mouse). It is released with the page.
        }

        private static void SendKeydown(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return;
            }

            if (_selfWeakReference?.TryGetTarget(out var self) == true)
            {
                self?._mediaPlayer?.Command(["keydown", keyName]);
            }
        }

        private static void SendKeyup(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return;
            }

            if (_selfWeakReference?.TryGetTarget(out var self) == true)
            {
                self?._mediaPlayer?.Command(["keyup", keyName]);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static LRESULT MessageHookProc(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (!_suppressKeyboard && nCode == 0)
            {
                uint flags = (uint)lParam.Value;
                uint vkey = (uint)wParam.Value;

                if ((flags & (1U << 31)) != 0)
                {
                    HandleKeyUp(vkey);

                    if (vkey == (uint)VIRTUAL_KEY.VK_F10)
                    {
                        return (LRESULT)1;
                    }
                }
                else
                {
                    uint scancode = HIWORD(flags);
                    bool isSystemKey = (flags & (1U << 29)) != 0;
                    if (isSystemKey)
                    {
                        if (vkey == (uint)VIRTUAL_KEY.VK_SPACE)
                        {
                            return (LRESULT)0;
                        }

                        HandleKeyDown(vkey, scancode);

                        if (vkey == (uint)VIRTUAL_KEY.VK_F10)
                        {
                            return (LRESULT)1;
                        }
                    }
                    else
                    {
                        HandleKeyDown(vkey, scancode);
                    }
                }
            }

            if (_selfWeakReference?.TryGetTarget(out var self) == true)
            {
                if (self?._hHook is { IsNull: false } hHook)
                {
                    return CallNextHookEx(hHook, nCode, wParam, lParam);
                }
            }

            return (LRESULT)0;
        }


        private static void HandleKeyDown(uint vkey, uint scancode)
        {
            // When a UI slider owns focus, its arrow keys are the slider's own
            // input; forwarding them to mpv as well would seek twice.
            if (AppContext.UiFocusInSlider && IsSliderNavigationKey(vkey))
            {
                return;
            }

            int mpkey = W32Keyboard.mp_w32_vkey_to_mpkey((int)vkey, (scancode & KF_EXTENDED) != 0);
            if (mpkey == 0)
            {
                mpkey = W32Common.decode_key(vkey, scancode & (0xff | KF_EXTENDED));
                if (mpkey == 0)
                {
                    return;
                }
            }


            SendKeydown(ModPrefix() + $"0x{mpkey:X}");
        }

        private static bool IsSliderNavigationKey(uint vkey)
        {
            return vkey == (uint)VIRTUAL_KEY.VK_LEFT || vkey == (uint)VIRTUAL_KEY.VK_RIGHT
                || vkey == (uint)VIRTUAL_KEY.VK_UP || vkey == (uint)VIRTUAL_KEY.VK_DOWN
                || vkey == (uint)VIRTUAL_KEY.VK_PRIOR || vkey == (uint)VIRTUAL_KEY.VK_NEXT
                || vkey == (uint)VIRTUAL_KEY.VK_HOME || vkey == (uint)VIRTUAL_KEY.VK_END;
        }

        private static void HandleKeyUp(uint key)
        {
            if (_logger.IsTraceEnabled)
            {
                _logger.Debug("keyup: key={}", key);
            }

            switch (key)
            {
                case (uint)VIRTUAL_KEY.VK_MENU:
                case (uint)VIRTUAL_KEY.VK_CONTROL:
                case (uint)VIRTUAL_KEY.VK_SHIFT:
                    break;
                default:
                {
                    SendKeyup($"0x{Keycodes.MP_INPUT_RELEASE_ALL:X}");
                    break;
                }
            }
        }

        private void SendAllKeyUp()
        {
            SendKeyup($"0x{Keycodes.MP_INPUT_RELEASE_ALL:X}");
        }

        private static string ModPrefix()
        {
            int mod = W32Common.mod_state();
            var prefix = "";

            if ((mod & Keycodes.MP_KEY_MODIFIER_SHIFT) != 0)
            {
                prefix += "Shift+";
            }

            if ((mod & Keycodes.MP_KEY_MODIFIER_CTRL) != 0)
            {
                prefix += "Ctrl+";
            }

            if ((mod & Keycodes.MP_KEY_MODIFIER_ALT) != 0)
            {
                prefix += "Alt+";
            }

            return prefix;
        }

    

    // ----- mouse forwarding (from MpvPlayerPage_Mouse.cs) -----
    private void VideoArea_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PlayerView);
        var props = point.Properties;

        var key = props.IsHorizontalMouseWheel
            ? (props.MouseWheelDelta > 0 ? "WHEEL_LEFT" : "WHEEL_RIGHT")
            : (props.MouseWheelDelta > 0 ? "WHEEL_UP" : "WHEEL_DOWN");

        SendMouseButton(key);
        e.Handled = true;
    }

    private void PlayerView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // input.conf: MBTN_LEFT_DBL cycle fullscreen
        SendMouseButton("MBTN_LEFT_DBL");
        e.Handled = true;
    }

    private static void SendMouseButton(string keyName)
    {
        if (_selfWeakReference?.TryGetTarget(out var self) == true)
        {
            self?._mediaPlayer?.Command(["keydown", keyName]);
            self?._mediaPlayer?.Command(["keyup", keyName]);
        }
        }
    }
}
