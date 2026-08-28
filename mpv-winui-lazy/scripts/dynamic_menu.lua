local utils = require 'mp.utils'
local msg = require 'mp.msg'

-- ===== 菜单本地化 =====
local menu_lang = mp.get_property_native('user-data/mpvw/language') or 'en-US'
if menu_lang == 'zh-TW' then menu_lang = 'zh-CN' end
-- BEGIN MENU_I18N
local menu_i18n = {
    ['en-US'] = {
        ['关闭'] = 'Off',
        ['关闭 VSR'] = 'Disable VSR',
        ['强制开启（仅 SDR 片源）'] = 'Force on (SDR source only)',
        ['清空所有脚本'] = 'Clear all scripts',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (SDR source + HDR screen)',
    },
    ['ja-JP'] = {
        ['关闭'] = 'オフ',
        ['关闭 VSR'] = 'VSRを無効化',
        ['强制开启（仅 SDR 片源）'] = '強制オン（SDRソースのみ）',
        ['清空所有脚本'] = '全スクリプトをクリア',
        ['自动（SDR 片源 + 屏幕 HDR）'] = '自動（SDRソース＋HDR画面）',
    },
    ['ko-KR'] = {
        ['关闭'] = '끄기',
        ['关闭 VSR'] = 'VSR 비활성화',
        ['强制开启（仅 SDR 片源）'] = '강제 켜기(SDR 소스만)',
        ['清空所有脚本'] = '모든 스크립트 지우기',
        ['自动（SDR 片源 + 屏幕 HDR）'] = '자동(SDR 소스 + HDR 화면)',
    },
    ['de-DE'] = {
        ['关闭'] = 'Aus',
        ['关闭 VSR'] = 'VSR deaktivieren',
        ['强制开启（仅 SDR 片源）'] = 'Erzwingen (nur SDR-Quelle)',
        ['清空所有脚本'] = 'Alle Skripte löschen',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Automatisch (SDR-Quelle + HDR-Bildschirm)',
    },
    ['fr-FR'] = {
        ['关闭'] = 'Désactivé',
        ['关闭 VSR'] = 'Désactiver VSR',
        ['强制开启（仅 SDR 片源）'] = 'Forcer (source SDR uniquement)',
        ['清空所有脚本'] = 'Effacer tous les scripts',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (source SDR + écran HDR)',
    },
    ['es-ES'] = {
        ['关闭'] = 'Apagado',
        ['关闭 VSR'] = 'Desactivar VSR',
        ['强制开启（仅 SDR 片源）'] = 'Forzar (solo fuente SDR)',
        ['清空所有脚本'] = 'Limpiar todos los scripts',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (fuente SDR + pantalla HDR)',
    },
    ['ru-RU'] = {
        ['关闭'] = 'Выкл',
        ['关闭 VSR'] = 'Отключить VSR',
        ['强制开启（仅 SDR 片源）'] = 'Принудительно (только SDR-источник)',
        ['清空所有脚本'] = 'Очистить все скрипты',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Авто (SDR-источник + HDR-экран)',
    },
}
-- END MENU_I18N

local function localize_title(title)
    if not title or title == '' then return title end
    local t = menu_i18n[menu_lang]
    if t and t[title] then return t[title] end
    return title
end

-- =========================================================================
--  配置区域 1：Nvidia VSR 滤镜（扁平子菜单，直接挂在“滤镜与增强”下）
-- =========================================================================
local vsr_menu_data = {
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

-- =========================================================================
--  配置区域 2：RTX Video HDR（扁平子菜单，直接挂在“滤镜与增强”下）
-- =========================================================================
local hdr_menu_data = {
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
            node.title = localize_title(item.title)
            node.submenu = build_json(item.items)
        else
            node.title = localize_title(item.title)
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
    local vsr_json = utils.format_json({ type = "submenu", submenu = build_json(vsr_menu_data) })
    mp.commandv('script-message-to', 'dyn_menu', 'update', 'vsr_menu', vsr_json)

    local hdr_json = utils.format_json({ type = "submenu", submenu = build_json(hdr_menu_data) })
    mp.commandv('script-message-to', 'dyn_menu', 'update', 'hdr_menu', hdr_json)

    local shader_json = utils.format_json({ type = "submenu", submenu = build_json(shader_menu_data) })
    mp.commandv('script-message-to', 'dyn_menu', 'update', 'shader_menu', shader_json)
end

mp.register_script_message('menu-ready', update_menus)
mp.observe_property("glsl-shaders", "native", update_menus)
mp.observe_property("vf", "native", update_menus)
mp.observe_property("user-data/hdr-auto/mode", "native", update_menus)
mp.observe_property('user-data/mpvw/language', 'string', function()
    menu_lang = mp.get_property_native('user-data/mpvw/language') or 'en-US'
    if menu_lang == 'zh-TW' then menu_lang = 'zh-CN' end
    update_menus()
end)
