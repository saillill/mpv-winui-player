/*
 * This file is part of mpv.
 *
 * mpv is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * mpv is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with mpv.  If not, see <http://www.gnu.org/licenses/>.
 */

using Windows.Win32.UI.Input.KeyboardAndMouse;
using static mpv.Keycodes;

namespace mpv
{
    public static class W32Keyboard
    {
        private record Keymap(int From, int To);

        private static readonly Keymap[] vk_map_ext =
        [
            // cursor keys
            new((int)VIRTUAL_KEY.VK_LEFT, MP_KEY_LEFT), new((int)VIRTUAL_KEY.VK_UP, MP_KEY_UP),
            new((int)VIRTUAL_KEY.VK_RIGHT, MP_KEY_RIGHT), new((int)VIRTUAL_KEY.VK_DOWN, MP_KEY_DOWN),

            // navigation block
            new((int)VIRTUAL_KEY.VK_INSERT, MP_KEY_INSERT), new((int)VIRTUAL_KEY.VK_DELETE, MP_KEY_DELETE),
            new((int)VIRTUAL_KEY.VK_HOME, MP_KEY_HOME), new((int)VIRTUAL_KEY.VK_END, MP_KEY_END),
            new((int)VIRTUAL_KEY.VK_PRIOR, MP_KEY_PAGE_UP), new((int)VIRTUAL_KEY.VK_NEXT, MP_KEY_PAGE_DOWN),

            // numpad independent of numlock
            new((int)VIRTUAL_KEY.VK_RETURN, MP_KEY_KPENTER),
        ];

        private static readonly Keymap[] vk_map =
        [
            // special keys
            new((int)VIRTUAL_KEY.VK_ESCAPE, MP_KEY_ESC), new((int)VIRTUAL_KEY.VK_BACK, MP_KEY_BS),
            new((int)VIRTUAL_KEY.VK_TAB, MP_KEY_TAB), new((int)VIRTUAL_KEY.VK_RETURN, MP_KEY_ENTER),
            new((int)VIRTUAL_KEY.VK_PAUSE, MP_KEY_PAUSE), new((int)VIRTUAL_KEY.VK_SLEEP, MP_KEY_SLEEP),
            new((int)VIRTUAL_KEY.VK_SNAPSHOT, MP_KEY_PRINT), new((int)VIRTUAL_KEY.VK_APPS, MP_KEY_MENU),

            // F-keys
            new((int)VIRTUAL_KEY.VK_F1, MP_KEY_F+1), new((int)VIRTUAL_KEY.VK_F2, MP_KEY_F+2),
            new((int)VIRTUAL_KEY.VK_F3, MP_KEY_F+3), new((int)VIRTUAL_KEY.VK_F4, MP_KEY_F+4),
            new((int)VIRTUAL_KEY.VK_F5, MP_KEY_F+5), new((int)VIRTUAL_KEY.VK_F6, MP_KEY_F+6),
            new((int)VIRTUAL_KEY.VK_F7, MP_KEY_F+7), new((int)VIRTUAL_KEY.VK_F8, MP_KEY_F+8),
            new((int)VIRTUAL_KEY.VK_F9, MP_KEY_F+9), new((int)VIRTUAL_KEY.VK_F10, MP_KEY_F+10),
            new((int)VIRTUAL_KEY.VK_F11, MP_KEY_F+11), new((int)VIRTUAL_KEY.VK_F12, MP_KEY_F+12),
            new((int)VIRTUAL_KEY.VK_F13, MP_KEY_F+13), new((int)VIRTUAL_KEY.VK_F14, MP_KEY_F+14),
            new((int)VIRTUAL_KEY.VK_F15, MP_KEY_F+15), new((int)VIRTUAL_KEY.VK_F16, MP_KEY_F+16),
            new((int)VIRTUAL_KEY.VK_F17, MP_KEY_F+17), new((int)VIRTUAL_KEY.VK_F18, MP_KEY_F+18),
            new((int)VIRTUAL_KEY.VK_F19, MP_KEY_F+19), new((int)VIRTUAL_KEY.VK_F20, MP_KEY_F+20),
            new((int)VIRTUAL_KEY.VK_F21, MP_KEY_F+21), new((int)VIRTUAL_KEY.VK_F22, MP_KEY_F+22),
            new((int)VIRTUAL_KEY.VK_F23, MP_KEY_F+23), new((int)VIRTUAL_KEY.VK_F24, MP_KEY_F+24),

            // numpad independent of numlock
            new((int)VIRTUAL_KEY.VK_SUBTRACT, MP_KEY_KPSUBTRACT),
            new((int)VIRTUAL_KEY.VK_ADD, MP_KEY_KPADD),
            new((int)VIRTUAL_KEY.VK_MULTIPLY, MP_KEY_KPMULTIPLY),
            new((int)VIRTUAL_KEY.VK_DIVIDE, MP_KEY_KPDIVIDE),

            // numpad with numlock
            new((int)VIRTUAL_KEY.VK_NUMPAD0, MP_KEY_KP0), new((int)VIRTUAL_KEY.VK_NUMPAD1, MP_KEY_KP1),
            new((int)VIRTUAL_KEY.VK_NUMPAD2, MP_KEY_KP2), new((int)VIRTUAL_KEY.VK_NUMPAD3, MP_KEY_KP3),
            new((int)VIRTUAL_KEY.VK_NUMPAD4, MP_KEY_KP4), new((int)VIRTUAL_KEY.VK_NUMPAD5, MP_KEY_KP5),
            new((int)VIRTUAL_KEY.VK_NUMPAD6, MP_KEY_KP6), new((int)VIRTUAL_KEY.VK_NUMPAD7, MP_KEY_KP7),
            new((int)VIRTUAL_KEY.VK_NUMPAD8, MP_KEY_KP8), new((int)VIRTUAL_KEY.VK_NUMPAD9, MP_KEY_KP9),
            new((int)VIRTUAL_KEY.VK_DECIMAL, MP_KEY_KPDEC),

            // numpad without numlock
            new((int)VIRTUAL_KEY.VK_INSERT, MP_KEY_KPINS), new((int)VIRTUAL_KEY.VK_END, MP_KEY_KPEND),
            new((int)VIRTUAL_KEY.VK_DOWN, MP_KEY_KPDOWN), new((int)VIRTUAL_KEY.VK_NEXT, MP_KEY_KPPGDOWN),
            new((int)VIRTUAL_KEY.VK_LEFT, MP_KEY_KPLEFT), new((int)VIRTUAL_KEY.VK_CLEAR, MP_KEY_KPBEGIN),
            new((int)VIRTUAL_KEY.VK_RIGHT, MP_KEY_KPRIGHT), new((int)VIRTUAL_KEY.VK_HOME, MP_KEY_KPHOME),
            new((int)VIRTUAL_KEY.VK_UP, MP_KEY_KPUP), new((int)VIRTUAL_KEY.VK_PRIOR, MP_KEY_KPPGUP),
            new((int)VIRTUAL_KEY.VK_DELETE, MP_KEY_KPDEL),
        ];

        private static int lookup_keymap(Keymap[] map, int key)
        {
            foreach (var entry in map)
            {
                if (entry.From == key)
                {
                    return entry.To;
                }
            }
            return 0;
        }

        public static int mp_w32_vkey_to_mpkey(int vkey, bool extended)
        {
            int mpkey = lookup_keymap(extended ? vk_map_ext : vk_map, vkey);
            if (extended && mpkey == 0)
            {
                mpkey = lookup_keymap(vk_map, vkey);
            }

            return mpkey;
        }

    }
}
