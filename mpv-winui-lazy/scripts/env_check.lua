--[[
env_check.lua — 环境体检（本地自定义，无上游）
检测 mpv 环境依赖：VapourSynth / k7sfunc / MediaInfo / thumbfast 子进程
mpv / shader 与 vs 模板目录，并把结果以 OSD 多行输出。
绑定：EnvCheck 键（input.conf 默认分配 Alt+E），或菜单 工具 > 环境体检。
--]]
local mp = require 'mp'
local utils = require 'mp.utils'
local msg = require 'mp.msg'

local function file_exists(path)
    if not path then return false end
    local f = io.open(path, 'rb')
    if f then f:close(); return true end
    return false
end

local function dir_exists(path)
    if not path then return false end
    local ok, err = pcall(utils.readdir, path)
    return ok and err ~= nil and true or false
end

local function which(cmd)
    local sep = package.config:sub(1, 1)
    local paths = os.getenv('PATH') or ''
    for dir in (paths .. (sep == '\\' and ';' or ':') .. os.getenv('PATHEXT') or ''):gmatch('[^' .. (sep == '\\' and ';' or ':') .. ']+') do
        for _, ext in ipairs({ '', '.exe', '.bat', '.cmd' }) do
            local candidate = dir .. sep .. cmd .. ext
            if file_exists(candidate) then return candidate end
        end
    end
    return nil
end

local function check_vapoursynth()
    -- VapourSynth: either the python module or the standalone CLI
    if which('vspipe') or which('vapoursynth') then
        return 'OK (CLI found)'
    end
    local ok = pcall(function() require('vapoursynth') end)
    if ok then return 'OK (python module)' end
    return 'MISSING (install VapourSynth + python)'
end

local function check_k7sfunc()
    local ok = pcall(function() require('k7sfunc') end)
    if ok then return 'OK' end
    return 'MISSING (install k7sfunc into the python site-packages)'
end

local function run()
    local lines = {}

    local mpv_dir = mp.get_property('config-dir') or ''
    local script_opts = mpv_dir .. '/script-opts'
    local scripts = mpv_dir .. '/scripts'
    local shaders = mpv_dir .. '/shaders'
    local vs_dir = mpv_dir .. '/vs'

    lines[#lines + 1] = '== 环境体检 =='
    local ver = mp.get_property('mpv-version') or '?'
    lines[#lines + 1] = 'mpv ' .. ver

    -- VapourSynth (vs/*.vpy templates)
    lines[#lines + 1] = 'VapourSynth : ' .. check_vapoursynth()
    lines[#lines + 1] = 'k7sfunc     : ' .. check_k7sfunc()
    lines[#lines + 1] = 'vs 模板     : ' .. (dir_exists(vs_dir) and #(utils.readdir(vs_dir) or {}) .. ' 个模板' or '目录缺失')

    -- thumbfast child mpv (spawns an independent mpv.exe)
    local exe_dir = mp.get_property('script-opts/thumbfast/max_width') ~= nil and mpv_dir or nil
    local child_mpv = which('mpv') or file_exists(mpv_dir .. '/mpv.exe') and (mpv_dir .. '/mpv.exe') or nil
    lines[#lines + 1] = 'thumbfast子mpv: ' .. (child_mpv and 'OK' or '未找到独立 mpv.exe（预览缩略图不可用）')

    -- MediaInfo (stats_mediainfo)
    local mediainfo = file_exists(mpv_dir .. '/MediaInfo.exe') or which('mediainfo')
    lines[#lines + 1] = 'MediaInfo   : ' .. (mediainfo and 'OK' or '未找到 MediaInfo.exe')

    -- shader / scripts dirs
    lines[#lines + 1] = 'shaders     : ' .. (dir_exists(shaders) and #(utils.readdir(shaders) or {}) .. ' 项' or '目录缺失')
    lines[#lines + 1] = 'scripts     : ' .. (dir_exists(scripts) and #(utils.readdir(scripts) or {}) .. ' 项' or '目录缺失')

    mp.osd_message(table.concat(lines, '\n'), 6)
end

mp.add_key_binding('Alt+e', 'EnvCheck', run)
mp.add_forced_key_binding('', 'EnvCheck', run)
msg.info('env_check loaded (Alt+E)')
