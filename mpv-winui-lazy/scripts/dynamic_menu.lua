local utils = require 'mp.utils'
local msg = require 'mp.msg'

-- =========================================================================
--  配置区域 1：VapourSynth 脚本菜单 (三级菜单结构)
-- =========================================================================
local vs_menu_data = {
    -- 1. Nvidia VSR
    {
        title = "Nvidia VSR",
        type = "submenu",
        items = {
            {
                title = "1.5x",
                type = "command",
                cmd = 'vf remove @vsr; vf pre @vsr:d3d11vpp=format=nv12:scale=1.5:scaling-mode=nvidia'
            },
            {
                title = "2.0x",
                type = "command",
                cmd = 'vf remove @vsr; vf pre @vsr:d3d11vpp=format=nv12:scale=2:scaling-mode=nvidia'
            },
            {
                title = "3.0x",
                type = "command",
                cmd = 'vf remove @vsr; vf pre @vsr:d3d11vpp=format=nv12:scale=3:scaling-mode=nvidia'
            },
            {
                title = "4.0x",
                type = "command",
                cmd = 'vf remove @vsr; vf pre @vsr:d3d11vpp=format=nv12:scale=4:scaling-mode=nvidia'
            },

            { title = "-", type = "separator" },

            {
                title = "关闭 VSR",
                type = "command",
                cmd = 'vf remove @vsr'
            },
        }
    },

    -- 2. RTX Video HDR（hdr_auto.lua：仅 SDR 片源 + 屏幕 HDR 时自动启用）
    {
        title = "RTX Video HDR",
        type = "submenu",
        items = {
            {
                title = "自动（SDR 片源 + 屏幕 HDR）",
                type = "toggle",
                cmd = 'script-message-to hdr_auto mode auto',
                check_prop = "user-data/hdr-auto/mode",
                check_val = "auto"
            },
            {
                title = "强制开启（仅 SDR 片源）",
                type = "toggle",
                cmd = 'script-message-to hdr_auto mode on',
                check_prop = "user-data/hdr-auto/mode",
                check_val = "on"
            },
            {
                title = "关闭",
                type = "toggle",
                cmd = 'script-message-to hdr_auto mode off',
                check_prop = "user-data/hdr-auto/mode",
                check_val = "off"
            },
        }
    },

    { title = "-", type = "separator" },

    -- 底部全局控制
    {
        title = "清空所有脚本",
        type = "command",
        cmd = 'vf clr ""'
    },
}

-- =========================================================================
--  配置区域 2：Shaders 滤镜菜单（精简：复杂滤镜已移除，仅保留 VSR/HDR）
-- =========================================================================
local shader_menu_data = {}

-- =========================================================================
--  核心逻辑
-- =========================================================================
local function is_active(prop_name, keyword)
    local prop = mp.get_property_native(prop_name)
    if not prop then return false end

    if prop_name == "user-data/hdr-auto/mode" then
        return tostring(prop) == keyword
    end

    if prop_name == "glsl-shaders" and type(prop) == "table" then
        for _, path in ipairs(prop) do
            if string.find(path, keyword, 1, true) then return true end
        end
    end

    if prop_name == "vf" and type(prop) == "table" then
        for _, filter in ipairs(prop) do
            if filter["name"] == "vapoursynth" then
                local file = filter["params"] and filter["params"]["file"]
                if file and string.find(file, keyword, 1, true) then return true end
            end
            if filter["name"] == "d3d11vpp" and filter["params"] and filter["params"]["nvidia-true-hdr"] ~= nil then
                if keyword == "nvidia-true-hdr" then return true end
            end
        end
    end

    return false
end

local function build_json(items)
    local json_items = {}
    for _, item in ipairs(items) do
        local node = {}
        if item.type == "separator" then
            node.type = "separator"
        elseif item.type == "submenu" then
            node.type = "submenu"
            node.title = item.title
            node.submenu = build_json(item.items)
        else
            node.title = item.title
            node.cmd = item.cmd
            if item.state then
                node.state = item.state
            end
            if item.type == "toggle" and item.check_prop then
                if is_active(item.check_prop, item.check_val) then
                    node.state = {"checked"}
                else
                    node.state = {}
                end
            end
        end
        table.insert(json_items, node)
    end
    return json_items
end

local function update_menus()
    local vs_json = utils.format_json({ type = "submenu", submenu = build_json(vs_menu_data) })
    mp.commandv('script-message-to', 'dyn_menu', 'update', 'vs_menu', vs_json)

    local shader_json = utils.format_json({ type = "submenu", submenu = build_json(shader_menu_data) })
    mp.commandv('script-message-to', 'dyn_menu', 'update', 'shader_menu', shader_json)
end

mp.register_script_message('menu-ready', update_menus)
mp.observe_property("glsl-shaders", "native", update_menus)
mp.observe_property("vf", "native", update_menus)
mp.observe_property("user-data/hdr-auto/mode", "native", update_menus)
