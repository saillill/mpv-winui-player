#pragma once
#include <mpv/client.h>
#include <string>
#include <vector>

namespace winrt::mpv_winrt
{
    /// <summary>Internal menu tree node used during construction.</summary>
    struct MenuEntry
    {
        std::string title;
        std::string cmd;
        std::string type = "command"; // command | submenu | separator
        std::vector<std::string> state;
        std::vector<MenuEntry> children;
        std::string dynamic_source;
    };

    /// <summary>
    /// Native replacement for dyn_menu.lua: parses input.conf #menu:
    /// annotations and builds mpv's menu-data property without Lua.
    /// </summary>
    struct MpvMenuBuilder
    {
        static bool BuildAndSet(mpv_handle* mpv, std::string const& config_dir);

    private:
        static std::vector<MenuEntry> ParseInputConf(std::string const& path);
        static void ExpandDynamic(mpv_handle* mpv, std::vector<MenuEntry>& entries);
        static void BuildNodeList(std::vector<MenuEntry> const& entries, mpv_node& out);
    };
}
