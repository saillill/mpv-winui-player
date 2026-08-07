--[[
seek_hold.lua —— 本地自定义（无上游）

跳转/拖拽进度条期间，mpv 会临时摘掉 VSR / RTX HDR 滤镜（见 vsr_auto.lua、
hdr_auto.lua），输出分辨率随之在 源尺寸 与 滤镜后尺寸 之间切换；
若 auto-window-resize 仍为 yes，窗口会跟着自动缩放。

本脚本在 seek 进行中或暂停期间把 auto-window-resize 临时设为 no，
并在 seek 结束/恢复播放 1.5 秒后再还原，避免拖进度条时窗口忽大忽小。
]]

local saved = nil
local release_timer = nil

local function release_hold()
	release_timer = nil
	if saved ~= nil then
		mp.set_property("auto-window-resize", saved)
		saved = nil
	end
end

local function hold()
	if saved == nil then
		saved = mp.get_property("auto-window-resize")
		if saved == nil then
			saved = "yes"
		end
		mp.set_property("auto-window-resize", "no")
	end
	if release_timer then
		release_timer:kill()
		release_timer = nil
	end
end

local function update()
	local seeking = mp.get_property("seeking") == "yes"
	local paused = mp.get_property("pause") == "yes"

	if seeking or paused then
		hold()
	elseif saved ~= nil and release_timer == nil then
	-- 稍等片刻再还原：滤镜重挂触发的 VO 重配会延后约 1 秒才完成，
	-- 太早还原窗口仍会跳一下（实测 0.3s 不够，1.5s 稳定）
	release_timer = mp.add_timeout(1.5, release_hold)
	end
end

mp.observe_property("seeking", "native", update)
mp.observe_property("pause", "native", update)
mp.register_event("file-loaded", update)
