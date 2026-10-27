-- preview_api_bridge.lua
--
-- The consumer half of mpv's OSC Preview API, which README.md has promised
-- since the upstream fork: "Thumbnail preview is built in and also supports
-- plugins implementing the `osc-preview-api`."
--
-- What the API is (mpv manual, "OSC Preview API"): the OSC writes a request to
-- `user-data/osc/draw-preview` -- a map with x, y, w, h (the rectangle the
-- thumbnail must occupy), hover-sec (the timestamp to show), and an optional
-- `ass` decoration string. A thumbnailer may also watch
-- `user-data/osc/draw-request`. Whoever draws clears the property when the
-- pointer leaves.
--
-- This build ships `osc = no` (mpv.conf), because the WinUI control bar
-- replaces it, so nothing writes those properties in the default
-- configuration and this script stays dormant. It exists for the case the
-- README describes: a user who enables the built-in OSC (`osc=yes`) or runs a
-- plugin that speaks the same protocol.
--
-- Deliberately a *bridge*, not a thumbnailer. Rendering frames is what
-- MpvPreviewer and the two C# backends already do; a second implementation in
-- Lua would be the "手搓" the brief rules out. So this script does the one
-- thing neither side can do for itself: it surfaces the request where the
-- WinUI layer can see it, via `user-data/mpvw/preview-request`, and exposes a
-- `script-message` so a plugin can ask the app to draw instead of drawing on
-- mpv's OSD.
--
-- Licence: project-written, LGPL-2.1-or-later. No upstream.

local o = {
    -- Mirror the request into user-data/mpvw/preview-request so the WinUI
    -- layer can render it with the compositor rather than an OSD overlay.
    mirror_to_app = true,
    -- Log one line the first time a request arrives and the app has not
    -- claimed the channel. Off by default: it fires on every hover otherwise.
    trace = false,
}

local mp_options = require("mp.options")
mp_options.read_options(o, "preview_api_bridge")

local mirrored = false

local function clear()
    if mirrored then
        mp.set_property_native("user-data/mpvw/preview-request", nil)
        mirrored = false
    end
end

local function on_draw_preview(_, value)
    -- nil is how the OSC says "the pointer left"; pass that through.
    if value == nil then
        clear()
        return
    end

    if type(value) ~= "table" then
        return
    end

    local x, y, w, h = value.x, value.y, value.w, value.h
    local sec = value["hover-sec"]

    -- A request without a rectangle is not drawable; a request without a
    -- timestamp predates the 59c91de move, so fall back to the standalone
    -- property for mpv builds older than that.
    if not (x and y and w and h) then
        return
    end
    if sec == nil then
        sec = mp.get_property_number("user-data/osc/hover-sec")
    end
    if sec == nil then
        return
    end

    if o.mirror_to_app then
        local payload = {
            x = math.floor(x), y = math.floor(y),
            w = math.floor(w), h = math.floor(h),
            ["hover-sec"] = sec,
        }
        if value.ass then
            payload.ass = value.ass
        end
        mp.set_property_native("user-data/mpvw/preview-request", payload)
        mirrored = true
    end

    if o.trace then
        mp.msg.info(string.format("preview request %dx%d+%d+%d @ %.3fs",
            w, h, x, y, sec))
    end
end

mp.observe_property("user-data/osc/draw-preview", "native", on_draw_preview)

-- A plugin that wants the frame drawn by the app rather than by mpv's OSD can
-- send this instead of writing draw-preview itself. It is the same shape, so
-- one consumer handles both.
mp.register_script_message("preview-request", function(json)
    local ok, payload = pcall(require("mp.utils").parse_json, json)
    if not ok or type(payload) ~= "table" then
        mp.msg.warn("preview-request: not JSON")
        return
    end
    on_draw_preview(nil, payload)
end)

mp.register_script_message("preview-clear", clear)

mp.add_hook("on_unload", 50, clear)
mp.register_event("shutdown", clear)
