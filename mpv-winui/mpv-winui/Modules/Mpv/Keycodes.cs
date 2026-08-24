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

using System;
using System.Text;

namespace mpv
{
    // Keys in the range [0, MP_KEY_BASE) follow unicode.
    // Special keys come after this.
    public static class Keycodes
    {
        public const int MP_KEY_BASE = 1 << 21;

        public static bool MP_KEY_IS_UNICODE(int key) => key >= 32 && key <= 0x10FFFF;

        public const int MP_KEY_ENTER = 13;
        public const int MP_KEY_TAB = 9;

        /* Control keys */
        public const int MP_KEY_BACKSPACE = MP_KEY_BASE + 0;
        public const int MP_KEY_DELETE = MP_KEY_BASE + 1;
        public const int MP_KEY_INSERT = MP_KEY_BASE + 2;
        public const int MP_KEY_HOME = MP_KEY_BASE + 3;
        public const int MP_KEY_END = MP_KEY_BASE + 4;
        public const int MP_KEY_PAGE_UP = MP_KEY_BASE + 5;
        public const int MP_KEY_PAGE_DOWN = MP_KEY_BASE + 6;
        public const int MP_KEY_ESC = MP_KEY_BASE + 7;
        public const int MP_KEY_PRINT = MP_KEY_BASE + 8;

        /* Control keys short name */
        public const int MP_KEY_BS = MP_KEY_BACKSPACE;
        public const int MP_KEY_DEL = MP_KEY_DELETE;
        public const int MP_KEY_INS = MP_KEY_INSERT;
        public const int MP_KEY_PGUP = MP_KEY_PAGE_UP;
        public const int MP_KEY_PGDOWN = MP_KEY_PAGE_DOWN;
        public const int MP_KEY_PGDWN = MP_KEY_PAGE_DOWN;

        /* Cursor movement */
        public const int MP_KEY_CRSR = MP_KEY_BASE + 0x10;
        public const int MP_KEY_RIGHT = MP_KEY_CRSR + 0;
        public const int MP_KEY_LEFT = MP_KEY_CRSR + 1;
        public const int MP_KEY_DOWN = MP_KEY_CRSR + 2;
        public const int MP_KEY_UP = MP_KEY_CRSR + 3;

        /* Multimedia/internet keyboard/remote keys */
        public const int MP_KEY_MM_BASE = MP_KEY_BASE + 0x20;
        public const int MP_KEY_POWER = MP_KEY_MM_BASE + 0;
        public const int MP_KEY_MENU = MP_KEY_MM_BASE + 1;
        public const int MP_KEY_PLAY = MP_KEY_MM_BASE + 2;
        public const int MP_KEY_PAUSE = MP_KEY_MM_BASE + 3;
        public const int MP_KEY_PLAYPAUSE = MP_KEY_MM_BASE + 4;
        public const int MP_KEY_STOP = MP_KEY_MM_BASE + 5;
        public const int MP_KEY_FORWARD = MP_KEY_MM_BASE + 6;
        public const int MP_KEY_REWIND = MP_KEY_MM_BASE + 7;
        public const int MP_KEY_NEXT = MP_KEY_MM_BASE + 8;
        public const int MP_KEY_PREV = MP_KEY_MM_BASE + 9;
        public const int MP_KEY_VOLUME_UP = MP_KEY_MM_BASE + 10;
        public const int MP_KEY_VOLUME_DOWN = MP_KEY_MM_BASE + 11;
        public const int MP_KEY_MUTE = MP_KEY_MM_BASE + 12;
        public const int MP_KEY_HOMEPAGE = MP_KEY_MM_BASE + 13;
        public const int MP_KEY_WWW = MP_KEY_MM_BASE + 14;
        public const int MP_KEY_MAIL = MP_KEY_MM_BASE + 15;
        public const int MP_KEY_FAVORITES = MP_KEY_MM_BASE + 16;
        public const int MP_KEY_SEARCH = MP_KEY_MM_BASE + 17;
        public const int MP_KEY_SLEEP = MP_KEY_MM_BASE + 18;
        public const int MP_KEY_CANCEL = MP_KEY_MM_BASE + 19;
        public const int MP_KEY_RECORD = MP_KEY_MM_BASE + 20;
        public const int MP_KEY_CHANNEL_UP = MP_KEY_MM_BASE + 21;
        public const int MP_KEY_CHANNEL_DOWN = MP_KEY_MM_BASE + 22;
        public const int MP_KEY_PLAYONLY = MP_KEY_MM_BASE + 23;
        public const int MP_KEY_PAUSEONLY = MP_KEY_MM_BASE + 24;
        public const int MP_KEY_GO_BACK = MP_KEY_MM_BASE + 25;
        public const int MP_KEY_GO_FORWARD = MP_KEY_MM_BASE + 26;
        public const int MP_KEY_TOOLS = MP_KEY_MM_BASE + 27;
        public const int MP_KEY_ZOOMIN = MP_KEY_MM_BASE + 28;
        public const int MP_KEY_ZOOMOUT = MP_KEY_MM_BASE + 29;

        /* Function keys */
        public const int MP_KEY_F = MP_KEY_BASE + 0x40;

        /* Keypad keys */
        public const int MP_KEY_KEYPAD = MP_KEY_BASE + 0x60;
        public const int MP_KEY_KP0 = MP_KEY_KEYPAD + 0;
        public const int MP_KEY_KP1 = MP_KEY_KEYPAD + 1;
        public const int MP_KEY_KP2 = MP_KEY_KEYPAD + 2;
        public const int MP_KEY_KP3 = MP_KEY_KEYPAD + 3;
        public const int MP_KEY_KP4 = MP_KEY_KEYPAD + 4;
        public const int MP_KEY_KP5 = MP_KEY_KEYPAD + 5;
        public const int MP_KEY_KP6 = MP_KEY_KEYPAD + 6;
        public const int MP_KEY_KP7 = MP_KEY_KEYPAD + 7;
        public const int MP_KEY_KP8 = MP_KEY_KEYPAD + 8;
        public const int MP_KEY_KP9 = MP_KEY_KEYPAD + 9;
        public const int MP_KEY_KPDEC = MP_KEY_KEYPAD + 10;
        public const int MP_KEY_KPINS = MP_KEY_KEYPAD + 11;
        public const int MP_KEY_KPDEL = MP_KEY_KEYPAD + 12;
        public const int MP_KEY_KPENTER = MP_KEY_KEYPAD + 13;
        public const int MP_KEY_KPHOME = MP_KEY_KEYPAD + 14;
        public const int MP_KEY_KPEND = MP_KEY_KEYPAD + 15;
        public const int MP_KEY_KPPGUP = MP_KEY_KEYPAD + 16;
        public const int MP_KEY_KPPGDOWN = MP_KEY_KEYPAD + 17;
        public const int MP_KEY_KPRIGHT = MP_KEY_KEYPAD + 18;
        public const int MP_KEY_KPLEFT = MP_KEY_KEYPAD + 19;
        public const int MP_KEY_KPDOWN = MP_KEY_KEYPAD + 20;
        public const int MP_KEY_KPUP = MP_KEY_KEYPAD + 21;
        public const int MP_KEY_KPBEGIN = MP_KEY_KEYPAD + 22;
        public const int MP_KEY_KPADD = MP_KEY_KEYPAD + 23;
        public const int MP_KEY_KPSUBTRACT = MP_KEY_KEYPAD + 24;
        public const int MP_KEY_KPMULTIPLY = MP_KEY_KEYPAD + 25;
        public const int MP_KEY_KPDIVIDE = MP_KEY_KEYPAD + 26;

        /* Mouse events from VOs */
        public const int MP_NO_REPEAT_KEY = 1 << 23;
        public const int MP_KEY_EMIT_ON_UP = 1 << 22;

        public static readonly int MP_MBTN_BASE = (MP_KEY_BASE + 0xA0) | MP_NO_REPEAT_KEY | MP_KEY_EMIT_ON_UP;
        public const int MP_MBTN_LEFT = 0/* filled below */;
        public const int MP_MBTN_MID = 1;
        public const int MP_MBTN_RIGHT = 2;
        public const int MP_WHEEL_UP = 3;
        public const int MP_WHEEL_DOWN = 4;
        public const int MP_WHEEL_LEFT = 5;
        public const int MP_WHEEL_RIGHT = 6;
        public const int MP_MBTN_BACK = 7;
        public const int MP_MBTN_FORWARD = 8;
        public const int MP_MBTN9 = 9;
        public const int MP_MBTN10 = 10;
        public const int MP_MBTN11 = 11;
        public const int MP_MBTN12 = 12;
        public const int MP_MBTN13 = 13;
        public const int MP_MBTN14 = 14;
        public const int MP_MBTN15 = 15;
        public const int MP_MBTN16 = 16;
        public const int MP_MBTN17 = 17;
        public const int MP_MBTN18 = 18;
        public const int MP_MBTN19 = 19;
        public const int MP_MBTN_END_OFFSET = 20;

        // Computed mouse button constants
        public static readonly int MP_MBTN_LEFT_ABS = MP_MBTN_BASE + MP_MBTN_LEFT;
        public static readonly int MP_MBTN_MID_ABS = MP_MBTN_BASE + MP_MBTN_MID;
        public static readonly int MP_MBTN_RIGHT_ABS = MP_MBTN_BASE + MP_MBTN_RIGHT;
        public static readonly int MP_WHEEL_UP_ABS = MP_MBTN_BASE + MP_WHEEL_UP;
        public static readonly int MP_WHEEL_DOWN_ABS = MP_MBTN_BASE + MP_WHEEL_DOWN;
        public static readonly int MP_WHEEL_LEFT_ABS = MP_MBTN_BASE + MP_WHEEL_LEFT;
        public static readonly int MP_WHEEL_RIGHT_ABS = MP_MBTN_BASE + MP_WHEEL_RIGHT;
        public static readonly int MP_MBTN_BACK_ABS = MP_MBTN_BASE + MP_MBTN_BACK;
        public static readonly int MP_MBTN_FORWARD_ABS = MP_MBTN_BASE + MP_MBTN_FORWARD;
        public static readonly int MP_MBTN9_ABS = MP_MBTN_BASE + MP_MBTN9;
        public static readonly int MP_MBTN10_ABS = MP_MBTN_BASE + MP_MBTN10;
        public static readonly int MP_MBTN11_ABS = MP_MBTN_BASE + MP_MBTN11;
        public static readonly int MP_MBTN12_ABS = MP_MBTN_BASE + MP_MBTN12;
        public static readonly int MP_MBTN13_ABS = MP_MBTN_BASE + MP_MBTN13;
        public static readonly int MP_MBTN14_ABS = MP_MBTN_BASE + MP_MBTN14;
        public static readonly int MP_MBTN15_ABS = MP_MBTN_BASE + MP_MBTN15;
        public static readonly int MP_MBTN16_ABS = MP_MBTN_BASE + MP_MBTN16;
        public static readonly int MP_MBTN17_ABS = MP_MBTN_BASE + MP_MBTN17;
        public static readonly int MP_MBTN18_ABS = MP_MBTN_BASE + MP_MBTN18;
        public static readonly int MP_MBTN19_ABS = MP_MBTN_BASE + MP_MBTN19;
        public static readonly int MP_MBTN_END = MP_MBTN_BASE + MP_MBTN_END_OFFSET;

        public static bool MP_KEY_IS_MOUSE_BTN_SINGLE(int code) =>
            code >= MP_MBTN_BASE && code < MP_MBTN_END;
        public static bool MP_KEY_IS_WHEEL(int code) =>
            code >= MP_WHEEL_UP_ABS && code <= MP_WHEEL_RIGHT_ABS;

        public static readonly int MP_MBTN_DBL_BASE = (MP_KEY_BASE + 0xC0) | MP_NO_REPEAT_KEY;
        public static readonly int MP_MBTN_LEFT_DBL = MP_MBTN_DBL_BASE + 0;
        public static readonly int MP_MBTN_MID_DBL = MP_MBTN_DBL_BASE + 1;
        public static readonly int MP_MBTN_RIGHT_DBL = MP_MBTN_DBL_BASE + 2;
        public static readonly int MP_MBTN_DBL_END = MP_MBTN_DBL_BASE + 20;

        public static bool MP_KEY_IS_MOUSE_BTN_DBL(int code) =>
            code >= MP_MBTN_DBL_BASE && code < MP_MBTN_DBL_END;

        public const int MP_KEY_MOUSE_BTN_COUNT = MP_MBTN_END_OFFSET;

        /* Tablet buttons */
        public const int MP_KEY_TABLET = MP_KEY_BASE + 0xD0;
        public const int MP_KEY_TABLET_TOOL_TIP = MP_KEY_TABLET + 1;
        public const int MP_KEY_TABLET_TOOL_STYLUS_BTN1 = MP_KEY_TABLET + 2;
        public const int MP_KEY_TABLET_TOOL_STYLUS_BTN2 = MP_KEY_TABLET + 3;
        public const int MP_KEY_TABLET_TOOL_STYLUS_BTN3 = MP_KEY_TABLET + 4;

        /* Game controller keys */
        public const int MP_KEY_GAMEPAD = MP_KEY_BASE + 0xF0;
        public const int MP_KEY_GAMEPAD_ACTION_DOWN = MP_KEY_GAMEPAD + 0;
        public const int MP_KEY_GAMEPAD_ACTION_RIGHT = MP_KEY_GAMEPAD + 1;
        public const int MP_KEY_GAMEPAD_ACTION_LEFT = MP_KEY_GAMEPAD + 2;
        public const int MP_KEY_GAMEPAD_ACTION_UP = MP_KEY_GAMEPAD + 3;
        public const int MP_KEY_GAMEPAD_BACK = MP_KEY_GAMEPAD + 4;
        public const int MP_KEY_GAMEPAD_MENU = MP_KEY_GAMEPAD + 5;
        public const int MP_KEY_GAMEPAD_START = MP_KEY_GAMEPAD + 6;
        public const int MP_KEY_GAMEPAD_LEFT_SHOULDER = MP_KEY_GAMEPAD + 7;
        public const int MP_KEY_GAMEPAD_RIGHT_SHOULDER = MP_KEY_GAMEPAD + 8;
        public const int MP_KEY_GAMEPAD_LEFT_TRIGGER = MP_KEY_GAMEPAD + 9;
        public const int MP_KEY_GAMEPAD_RIGHT_TRIGGER = MP_KEY_GAMEPAD + 10;
        public const int MP_KEY_GAMEPAD_LEFT_STICK = MP_KEY_GAMEPAD + 11;
        public const int MP_KEY_GAMEPAD_RIGHT_STICK = MP_KEY_GAMEPAD + 12;
        public const int MP_KEY_GAMEPAD_DPAD_UP = MP_KEY_GAMEPAD + 13;
        public const int MP_KEY_GAMEPAD_DPAD_DOWN = MP_KEY_GAMEPAD + 14;
        public const int MP_KEY_GAMEPAD_DPAD_LEFT = MP_KEY_GAMEPAD + 15;
        public const int MP_KEY_GAMEPAD_DPAD_RIGHT = MP_KEY_GAMEPAD + 16;
        public const int MP_KEY_GAMEPAD_LEFT_STICK_UP = MP_KEY_GAMEPAD + 17;
        public const int MP_KEY_GAMEPAD_LEFT_STICK_DOWN = MP_KEY_GAMEPAD + 18;
        public const int MP_KEY_GAMEPAD_LEFT_STICK_LEFT = MP_KEY_GAMEPAD + 19;
        public const int MP_KEY_GAMEPAD_LEFT_STICK_RIGHT = MP_KEY_GAMEPAD + 20;
        public const int MP_KEY_GAMEPAD_RIGHT_STICK_UP = MP_KEY_GAMEPAD + 21;
        public const int MP_KEY_GAMEPAD_RIGHT_STICK_DOWN = MP_KEY_GAMEPAD + 22;
        public const int MP_KEY_GAMEPAD_RIGHT_STICK_LEFT = MP_KEY_GAMEPAD + 23;
        public const int MP_KEY_GAMEPAD_RIGHT_STICK_RIGHT = MP_KEY_GAMEPAD + 24;

        /* Reserved area */
        public const int MP_KEY_UNKNOWN_RESERVED_START = MP_KEY_BASE + 0x10000;
        public const int MP_KEY_UNKNOWN_RESERVED_LAST = MP_KEY_BASE + 0x20000 - 1;

        /* Special keys */
        public const int MP_KEY_INTERN = MP_KEY_BASE + 0x20000;
        public const int MP_KEY_CLOSE_WIN = MP_KEY_INTERN + 0;
        public static readonly int MP_KEY_MOUSE_MOVE = (MP_KEY_INTERN + 1) | MP_NO_REPEAT_KEY;
        public static readonly int MP_KEY_MOUSE_LEAVE = (MP_KEY_INTERN + 2) | MP_NO_REPEAT_KEY;
        public static readonly int MP_KEY_MOUSE_ENTER = (MP_KEY_INTERN + 3) | MP_NO_REPEAT_KEY;

        public static bool MP_KEY_IS_MOUSE_CLICK(int code) =>
            MP_KEY_IS_MOUSE_BTN_SINGLE(code) || MP_KEY_IS_MOUSE_BTN_DBL(code);

        public static bool MP_KEY_IS_MOUSE_MOVE(int code) =>
            code == MP_KEY_MOUSE_MOVE || code == MP_KEY_MOUSE_ENTER || code == MP_KEY_MOUSE_LEAVE;

        public static bool MP_KEY_DEPENDS_ON_MOUSE_POS(int code) =>
            MP_KEY_IS_MOUSE_CLICK(code) || code == MP_KEY_MOUSE_MOVE;

        public static bool MP_KEY_IS_MOUSE(int code) =>
            MP_KEY_IS_MOUSE_CLICK(code) || MP_KEY_IS_MOUSE_MOVE(code);

        public const int MP_KEY_UNMAPPED = MP_KEY_INTERN + 4;
        public const int MP_KEY_ANY_UNICODE = MP_KEY_INTERN + 5;
        public const int MP_INPUT_RELEASE_ALL = MP_KEY_INTERN + 6;
        public const int MP_TOUCH_RELEASE_ALL = MP_KEY_INTERN + 7;

        /* Modifiers added to individual keys */
        public const int MP_KEY_MODIFIER_SHIFT = 1 << 24;
        public const int MP_KEY_MODIFIER_CTRL = 1 << 25;
        public const int MP_KEY_MODIFIER_ALT = 1 << 26;
        public const int MP_KEY_MODIFIER_META = 1 << 27;

        /* Flags for key events */
        public const int MP_KEY_STATE_DOWN = 1 << 28;
        public const int MP_KEY_STATE_UP = 1 << 29;
        public const int MP_KEY_STATE_SET_ONLY = 1 << 30;

        public const int MP_KEY_MODIFIER_MASK = MP_KEY_MODIFIER_SHIFT | MP_KEY_MODIFIER_CTRL |
                                                 MP_KEY_MODIFIER_ALT | MP_KEY_MODIFIER_META |
                                                 MP_KEY_STATE_DOWN | MP_KEY_STATE_UP;
    }
}
