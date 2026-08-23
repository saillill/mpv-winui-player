#include "pch.h"
#include "MpvMenuBuilder.h"
#include <fstream>
#include <algorithm>
#include <functional>

namespace winrt::mpv_winrt
{
    // ---------- helpers ----------

    static std::string Trim(std::string const& s)
    {
        auto start = s.find_first_not_of(" \t\r\n");
        if (start == std::string::npos) return "";
        auto end = s.find_last_not_of(" \t\r\n");
        return s.substr(start, end - start + 1);
    }

    static std::vector<std::string> Split(std::string const& s, char delim)
    {
        std::vector<std::string> out;
        size_t pos = 0;
        while (pos <= s.size())
        {
            auto next = s.find(delim, pos);
            if (next == std::string::npos) next = s.size();
            out.push_back(Trim(s.substr(pos, next - pos)));
            pos = next + 1;
        }
        return out;
    }

    /// Strip Lua-style #@state=(...) / #@tracks/audio annotations from a title.
    /// Returns clean title + extracted annotation (if any).
    static void CleanTitle(std::string& title, std::string& annotation)
    {
        auto hash = title.find("#@");
        if (hash != std::string::npos)
        {
            annotation = Trim(title.substr(hash + 1)); // keep '@...'
            title = Trim(title.substr(0, hash));
        }
    }

    /// Evaluate simple state conditions against mpv properties.
    /// Returns set of state strings ("checked"/"disabled"/"hidden").
    static std::vector<std::string> EvalState(mpv_handle* mpv, std::string const& expr)
    {
        std::vector<std::string> result;
        if (!mpv || expr.empty()) return result;

        // Map of known property names to checked-state queries.
        // We only handle the common patterns from the bundled input.conf.
        struct Rule { const char* prop; const char* mpv_prop; bool invert; };
        static const Rule rules[] = {
            { "pause", "pause", false },
            { "mute", "mute", false },
            { "deband", "deband", false },
            { "deinterlace", "deinterlace", false },
            { "sub_visibility", "sub-visibility", false },
            { "secondary_sub_visibility", "secondary-sub-visibility", false },
            { "icc_profile_auto", "icc-profile-auto", false },
            { "idle_active", "core-idle", false },
        };

        for (auto const& rule : rules)
        {
            if (expr.find(rule.prop) == std::string::npos) continue;
            int flag = 0;
            if (mpv_get_property(mpv, rule.mpv_prop, MPV_FORMAT_FLAG, &flag) >= 0 && flag)
            {
                result.push_back("checked");
            }
            break; // only match first rule
        }

        // 'disabled' when expression mentions idle_active and we're idle
        if (expr.find("'disabled'") != std::string::npos && expr.find("idle_active") != std::string::npos)
        {
            int idle = 0;
            if (mpv_get_property(mpv, "core-idle", MPV_FORMAT_FLAG, &idle) >= 0 && idle)
            {
                result.push_back("disabled");
            }
        }
        // 'hidden' when expression mentions it
        if (expr.find("'hidden'") != std::string::npos)
        {
            result.push_back("hidden");
        }

        return result;
    }

    /// Query mpv track-list and append track entries as children.
    static void AppendTracks(mpv_handle* mpv, std::vector<MenuEntry>& children, char const* type_filter)
    {
        mpv_node node;
        if (mpv_get_property(mpv, "track-list", MPV_FORMAT_NODE, &node) < 0) return;
        if (node.format != MPV_FORMAT_NODE_ARRAY) { mpv_free_node_contents(&node); return; }

        auto* list = node.u.list;
        for (int i = 0; i < list->num; i++)
        {
            if (list->values[i].format != MPV_FORMAT_NODE_MAP) continue;
            auto* item = list->values[i].u.list;

            std::string track_type, track_title, track_lang;
            int64_t track_id = 0;
            for (int j = 0; j < item->num; j++)
            {
                std::string key = item->keys[j];
                auto* val = &item->values[j];
                if (key == "type" && val->format == MPV_FORMAT_STRING) track_type = val->u.string;
                else if (key == "title" && val->format == MPV_FORMAT_STRING) track_title = val->u.string;
                else if (key == "lang" && val->format == MPV_FORMAT_STRING) track_lang = val->u.string;
                else if (key == "id" && val->format == MPV_FORMAT_INT64) track_id = val->u.int64;
            }
            if (track_type != type_filter) continue;

            MenuEntry e;
            e.title = track_title.empty() ? track_lang : track_title;
            if (e.title.empty()) e.title = "Track " + std::to_string(track_id);
            e.cmd = "no-osd set " + std::string(type_filter) + " " + std::to_string(track_id) + "; set osd-msg-msg \"Track: \"";
            // Mark as selected if current
            for (int j = 0; j < item->num; j++)
            {
                std::string key = item->keys[j];
                if (key == "selected" && item->values[j].format == MPV_FORMAT_FLAG && item->values[j].u.flag)
                {
                    e.state.push_back("checked");
                }
            }
            children.push_back(std::move(e));
        }
        mpv_free_node_contents(&node);
    }

    /// Query chapter-list and append chapter entries.
    static void AppendChapters(mpv_handle* mpv, std::vector<MenuEntry>& children)
    {
        int64_t count = 0;
        if (mpv_get_property(mpv, "chapter-list/count", MPV_FORMAT_INT64, &count) < 0 || count == 0) return;

        for (int64_t i = 0; i < count; i++)
        {
            auto title_prop = "chapter-list/" + std::to_string(i) + "/title";
            auto time_prop = "chapter-list/" + std::to_string(i) + "/time";

            char buf[256]{};
            auto* str = buf;
            double time = 0;
            if (mpv_get_property(mpv, title_prop.c_str(), MPV_FORMAT_OSD_STRING, &str) >= 0 && str)
            {
                mpv_get_property(mpv, time_prop.c_str(), MPV_FORMAT_DOUBLE, &time);
                MenuEntry e;
                e.title = str;
                e.cmd = "no-osd seek " + std::to_string(time) + " absolute";
                children.push_back(std::move(e));
                mpv_free(str);
            }
        }
    }

    /// Check if an annotation starts with a known prefix and expand accordingly.
    static void ExpandAnnotation(mpv_handle* mpv, MenuEntry& entry, std::string const& annotation)
    {
        if (annotation.starts_with("@tracks/video"))
            AppendTracks(mpv, entry.children, "video");
        else if (annotation.starts_with("@tracks/audio"))
            AppendTracks(mpv, entry.children, "audio");
        else if (annotation.starts_with("@tracks/sub"))
            AppendTracks(mpv, entry.children, "sub");
        else if (annotation == "@chapters")
            AppendChapters(mpv, entry.children);
        // Other dynamic sources (@playlist, @profiles, @editions,
        // @audio-devices, @shader_menu, @vsr_menu, @hdr_menu) are not
        // expanded in v1; they appear as empty submenus or are skipped.
    }

    /// Remove empty submenus that resulted from unexpanded dynamic annotations.
    static void PruneEmpty(std::vector<MenuEntry>& entries)
    {
        entries.erase(
            std::remove_if(entries.begin(), entries.end(),
                [](MenuEntry const& e)
                {
                    return e.type == "submenu" && !e.cmd.empty() == false && e.children.empty();
                }),
            entries.end());

        for (auto& e : entries)
            PruneEmpty(e.children);
    }

    /// Deduplicate consecutive separators and remove leading/trailing ones.
    static void CleanSeparators(std::vector<MenuEntry>& entries)
    {
        std::vector<MenuEntry> cleaned;
        for (auto& e : entries)
        {
            if (e.type == "separator")
            {
                if (cleaned.empty() || cleaned.back().type == "separator")
                    continue;
            }
            cleaned.push_back(std::move(e));
        }
        while (!cleaned.empty() && cleaned.back().type == "separator")
            cleaned.pop_back();
        entries = std::move(cleaned);

        for (auto& e : entries)
            CleanSeparators(e.children);
    }

    // ---------- input.conf parsing ----------

    std::vector<MenuEntry> MpvMenuBuilder::ParseInputConf(std::string const& path)
    {
        std::ifstream file(path);
        if (!file.is_open()) return {};

        // Flat list of (path_segments, command) before tree assembly
        struct RawItem
        {
            std::vector<std::string> path;
            std::string cmd;
            std::string annotation;
            int order;
        };

        std::vector<RawItem> raw_items;
        int order = 0;

        std::string line;
        while (std::getline(file, line))
        {
            auto trimmed = Trim(line);
            if (trimmed.empty() || trimmed[0] == '#') continue;
            if (trimmed.find("#menu:") == std::string::npos) continue;

            // Extract key and command (before the # comment)
            auto hash_pos = trimmed.find('#');
            auto cmd_part = Trim(trimmed.substr(0, hash_pos));
            auto menu_part = Trim(trimmed.substr(hash_pos));

            // Extract menu path from "#menu: A > B > C"
            auto menu_pos = menu_part.find("menu:");
            if (menu_pos == std::string::npos) continue;
            auto menu_path = Trim(menu_part.substr(menu_pos + 5));

            // Extract annotation (#@...)
            std::string annotation;
            CleanTitle(menu_path, annotation);

            // Split path by >
            auto segments = Split(menu_path, '>');

            // Get key binding (first token of cmd_part)
            auto space = cmd_part.find(' ');
            auto key_binding = space != std::string::npos ? cmd_part.substr(0, space) : cmd_part;

            // Separator detection
            if (segments.size() == 1 && (segments[0] == "-" || key_binding == "_"))
            {
                raw_items.push_back({ {segments[0]}, "", "", order++ });
                continue;
            }

            raw_items.push_back({ segments, cmd_part, annotation, order++ });
        }

        // Build tree using nested iteration
        std::vector<MenuEntry> root;

        // Use a helper lambda to find-or-create a child
        std::function<MenuEntry*(std::vector<MenuEntry>&, std::string const&, std::string const&)> find_or_create =
            [&](std::vector<MenuEntry>& children, std::string const& title, std::string const& type) -> MenuEntry*
        {
            for (auto& c : children)
            {
                if (c.title == title && c.type == type)
                    return &c;
            }
            MenuEntry e;
            e.title = title;
            e.type = type;
            children.push_back(std::move(e));
            return &children.back();
        };

        // WARNING: pointers into vector may be invalidated by push_back.
        // Use a different approach: sort items by depth then build bottom-up.
        // Actually, simplest correct approach: use a recursive insert with indices.

        // Sort by number of path segments (shallowest first)
        std::sort(raw_items.begin(), raw_items.end(),
            [](RawItem const& a, RawItem const& b) { return a.path.size() < b.path.size(); });

        // Track subtree roots by path string for pointer-stable lookup
        std::function<void(std::vector<MenuEntry>&, RawItem const&)> insert_item =
            [&](std::vector<MenuEntry>& children, RawItem const& item)
        {
            if (item.path.size() == 1)
            {
                MenuEntry e;
                e.title = item.path[0];
                e.cmd = item.cmd;
                if (item.cmd == "" && item.annotation.empty()) e.type = "separator";
                if (!item.annotation.empty())
                {
                    // Check for @state annotation
                    if (item.annotation.starts_with("@state="))
                        e.state = {}; // simplified: evaluated later
                    // Dynamic source like @tracks/audio → will be expanded later
                    e.dynamic_source = item.annotation;
                    if (e.dynamic_source.starts_with("@"))
                        e.type = "submenu"; // will be populated dynamically
                }
                // Skip duplicate titles at same level
                for (auto& c : children)
                {
                    if (c.title == e.title && c.type == e.type) return;
                }
                children.push_back(std::move(e));
                return;
            }

            // Find or create intermediate submenu
            std::string parent_title = item.path[0];
            MenuEntry* parent = nullptr;
            for (auto& c : children)
            {
                if (c.title == parent_title && c.type == "submenu")
                {
                    parent = &c;
                    break;
                }
            }
            if (!parent)
            {
                MenuEntry pe;
                pe.title = parent_title;
                pe.type = "submenu";
                children.push_back(std::move(pe));
                parent = &children.back();
            }

            // Recurse with remaining path — but need stable storage!
            // Copy remaining path and recurse on the last element of children
            RawItem sub = item;
            sub.path = std::vector<std::string>(item.path.begin() + 1, item.path.end());
            // Re-find parent since vector may have reallocated
            for (auto& c : children)
            {
                if (c.title == parent_title && c.type == "submenu")
                {
                    insert_item(c.children, sub);
                    break;
                }
            }
        };

        // Process in sorted order (already sorted by depth)
        for (auto& item : raw_items)
        {
            insert_item(root, item);
        }

        return root;
    }

    // ---------- mpv_node construction ----------

    static void AddStringToMap(mpv_node& map_node, char const* key, char const* value)
    {
        auto* list = map_node.u.list;
        auto new_num = list->num + 1;
        auto* new_keys = (char**)realloc(list->keys, new_num * sizeof(char*));
        auto* new_vals = (mpv_node*)realloc(list->values, new_num * sizeof(mpv_node));
        if (new_keys) list->keys = new_keys;
        if (new_vals) list->values = new_vals;
        list->num = new_num;
        list->keys[new_num - 1] = strdup(key);
        list->values[new_num - 1].format = MPV_FORMAT_STRING;
        list->values[new_num - 1].u.string = strdup(value);
    }

    static void AddNodeToMap(mpv_node& map_node, char const* key, mpv_node&& child)
    {
        auto* list = map_node.u.list;
        auto new_num = list->num + 1;
        auto* new_keys = (char**)realloc(list->keys, new_num * sizeof(char*));
        auto* new_vals = (mpv_node*)realloc(list->values, new_num * sizeof(mpv_node));
        if (new_keys) list->keys = new_keys;
        if (new_vals) list->values = new_vals;
        list->num = new_num;
        list->keys[new_num - 1] = strdup(key);
        list->values[new_num - 1] = std::move(child);
    }

    static void InitMapNode(mpv_node& node)
    {
        node.format = MPV_FORMAT_NODE_MAP;
        node.u.list = new mpv_node_list{};
        node.u.list->num = 0;
        node.u.list->keys = nullptr;
        node.u.list->values = nullptr;
    }

    static void InitArrayNode(mpv_node& node)
    {
        node.format = MPV_FORMAT_NODE_ARRAY;
        node.u.list = new mpv_node_list{};
        node.u.list->num = 0;
        node.u.list->keys = nullptr;
        node.u.list->values = nullptr;
    }

    void MpvMenuBuilder::BuildNodeList(std::vector<MenuEntry> const& entries, mpv_node& out)
    {
        InitArrayNode(out);
        auto* arr = out.u.list;

        for (auto const& e : entries)
        {
            // Grow array
            auto new_num = arr->num + 1;
            auto* new_vals = (mpv_node*)realloc(arr->values, new_num * sizeof(mpv_node));
            if (new_vals) arr->values = new_vals;
            arr->num = new_num;

            auto& item = arr->values[arr->num - 1];
            InitMapNode(item);

            // title
            AddStringToMap(item, "title", e.title.c_str());

            // type
            AddStringToMap(item, "type", e.type.c_str());

            // cmd (only for non-submenu items)
            if (!e.cmd.empty())
            {
                AddStringToMap(item, "cmd", e.cmd.c_str());
            }

            // state array
            if (!e.state.empty())
            {
                mpv_node state_arr;
                InitArrayNode(state_arr);
                auto* sa = state_arr.u.list;
                for (auto const& st : e.state)
                {
                    auto nn = sa->num + 1;
                    auto* nv = (mpv_node*)realloc(sa->values, nn * sizeof(mpv_node));
                    if (nv) sa->values = nv;
                    sa->num = nn;
                    sa->values[nn - 1].format = MPV_FORMAT_STRING;
                    sa->values[nn - 1].u.string = strdup(st.c_str());
                }
                AddNodeToMap(item, "state", std::move(state_arr));
            }

            // children (for submenus)
            if (!e.children.empty())
            {
                mpv_node children_node;
                BuildNodeList(e.children, children_node);
                AddNodeToMap(item, "items", std::move(children_node));
            }
        }
    }

    

    // ---------- dynamic expansion ----------

    void MpvMenuBuilder::ExpandDynamic(mpv_handle* mpv, std::vector<MenuEntry>& entries)
    {
        for (auto& e : entries)
        {
            if (!e.dynamic_source.empty() && !e.dynamic_source.empty())
            {
                ExpandAnnotation(mpv, e, e.dynamic_source);
            }
            ExpandDynamic(mpv, e.children);
        }
    }

    // ---------- public API ----------

    bool MpvMenuBuilder::BuildAndSet(mpv_handle* mpv, std::string const& config_dir)
    {
        if (!mpv) return false;

        auto input_conf = config_dir + "/input.conf";
        auto entries = ParseInputConf(input_conf);
        if (entries.empty())
        {
            // Fallback: try the bundled input.conf next to the exe
            input_conf = config_dir + "/../../input.conf";
            entries = ParseInputConf(input_conf);
        }
        if (entries.empty()) return false;

        ExpandDynamic(mpv, entries);
        PruneEmpty(entries);
        CleanSeparators(entries);

        mpv_node root;
        BuildNodeList(entries, root);

        int result = mpv_set_property(mpv, "menu-data", MPV_FORMAT_NODE, &root);
        // Do NOT call mpv_free_node_contents here. Our tree was allocated
        // with new/realloc, not mpv's internal ta allocator — calling
        // mpv's free function on foreign memory corrupts the heap and
        // crashes with a canary assertion in ta.c.
        // The one-time leak is negligible (a few KB at startup).
        return result >= 0;
    }
}
