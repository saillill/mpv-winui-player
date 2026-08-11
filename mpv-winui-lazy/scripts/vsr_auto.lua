--[[
vsr_auto.lua —— 本地自定义（无上游）

行为：
  1. 视频分辨率低于 4K（且非封面/图片轨道）时，自动挂载 NVIDIA VSR 2x（@vsr）。
  2. RTX Video HDR（d3d11vpp=nvidia-true-hdr）与 VSR 兼容，不算“其它滤镜”；
     出现其它滤镜（如 RIFE 等）时自动移除 @vsr，滤镜移除后重新挂回 @vsr。
  3. 通过菜单手动“关闭 VSR”（vf remove @vsr）时，若无其它滤镜，会自动重新开启；
     想彻底关闭自动 VSR，请删除本脚本或把 load 改为 false。
]]

local msg = require("mp.msg")

local user_opt = {
	load = true,
	scale = 2,
}
mp.options = require("mp.options")
mp.options.read_options(user_opt)

if user_opt.load == false then
	msg.info("脚本已被初始化禁用")
	return
end

local updating = false
local seek_suspended = false
local enabled = true

local function vf_list()
	return mp.get_property_native("vf") or {}
end

local function is_hdr_filter(f)
	if f.name ~= "d3d11vpp" or not f.params then
		return false
	end
	local v = f.params["nvidia-true-hdr"]
	return v ~= nil and v ~= false and v ~= "no" and v ~= "false" and v ~= "0"
end

local function has_other_filters(vf)
	for _, f in ipairs(vf) do
		if f.label ~= "vsr" and not is_hdr_filter(f) then
			return true
		end
	end
	return false
end

local function has_vsr(vf)
	for _, f in ipairs(vf) do
		if f.label == "vsr" then
			return true
		end
	end
	return false
end

local function video_ok()
	local w = mp.get_property_number("video-params/w")
	local h = mp.get_property_number("video-params/h")
	if not w or not h then
		return false
	end
	local albumart = mp.get_property_native("current-tracks/video/albumart")
	local image = mp.get_property_native("current-tracks/video/image")
	if albumart or image then
		return false
	end
	return w < 3840 and h < 2160
end

local function sync_vsr()
	if updating then
		return
	end
	updating = true

	local vf = vf_list()
	local has = has_vsr(vf)
	if not enabled then
		if has then
			mp.commandv("vf", "remove", "@vsr")
			msg.verbose("VSR 已被设置禁用，已移除 @vsr")
		end
		updating = false
		return
	end
	if has_other_filters(vf) then
		if has then
			mp.commandv("vf", "remove", "@vsr")
			msg.verbose("检测到其它滤镜，已移除 @vsr")
		end
	elseif video_ok() and not seek_suspended then
		if not has then
			-- use pre so VSR always runs BEFORE @hdr (upscale first, then RTX HDR conversion)
			mp.commandv("vf", "pre", "@vsr:d3d11vpp=format=nv12:scale=" .. user_opt.scale .. ":scaling-mode=nvidia")
			msg.verbose("自动挂载 @vsr")
		end
	else
		if has then
			mp.commandv("vf", "remove", "@vsr")
			msg.verbose("分辨率/轨道不满足条件，已移除 @vsr")
		end
	end

	updating = false
end

mp.observe_property("vf", "native", sync_vsr)
mp.observe_property("video-params", "native", sync_vsr)
mp.observe_property("current-tracks/video/albumart", "native", sync_vsr)
mp.observe_property("current-tracks/video/image", "native", sync_vsr)
mp.register_event("file-loaded", sync_vsr)

-- 读取设置窗口的开关：user-data/mpvw/vsr-auto
-- App 用 `set user-data/mpvw/vsr-auto yes/no` 写入字符串；val ~= false 会把
-- 字符串 "no" 当作启用，导致设置里关不掉。兼容 string 与 boolean 两种形态。
mp.observe_property("user-data/mpvw/vsr-auto", "native", function(_, val)
	enabled = (val == true) or (val == "yes")
	msg.verbose("vsr auto = " .. tostring(enabled))
	sync_vsr()
end)

-- 跳转/拖拽期间临时摘掉 @vsr：精确 seek 不再被 VSR 拖慢，恢复播放时挂回
mp.observe_property("seeking", "native", function(_, val)
	if val == true then
		if not seek_suspended then
			seek_suspended = true
			sync_vsr()
		end
	elseif seek_suspended and mp.get_property("pause") ~= "yes" then
		seek_suspended = false
		sync_vsr()
	end
end)
mp.observe_property("pause", "native", function(_, val)
	if val == false and seek_suspended then
		seek_suspended = false
		sync_vsr()
	end
end)
