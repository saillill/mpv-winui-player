-- mpvw_hdr_override.lua
-- 手动覆盖 App 报告的显示器 color-kind（App 检测不到 HDR 时使用）。
-- script-opts/mpvw_hdr_override.conf: mode= 留空=跟随 App；HDR=强制 HDR；SDR=强制 SDR
local o = { mode = "" }
mp.options = require("mp.options")
mp.options.read_options(o, "mpvw_hdr_override")

local function apply()
    if o.mode == "HDR" or o.mode == "SDR" then
        mp.set_property("user-data/mpvw/color-kind", o.mode)
    end
end

mp.observe_property("user-data/mpvw/color-kind", "native", apply)
apply()
