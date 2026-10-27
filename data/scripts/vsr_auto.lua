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
-- 窗口拖动缩放中（App 在 WM_ENTERSIZEMOVE/EXITSIZEMOVE 发布）
local window_resizing = false
-- An attach command was issued but the (async) vf list has not shown it yet.
local vsr_issued = false

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

-- 三态：true=满足 / false=明确不满足 / nil=暂时无法判断。
-- video-params 在滤镜链重配置的瞬间可能短暂不可用；旧实现把它当成 false，
-- 导致"摘除 @vsr → 链路重配置 → 参数恢复 → 再挂载"每 100~300ms 循环一次
-- （日志表现为 VO 720x480 <=> 1440x960 各 35 次），这也是拖动窗口时卡顿的来源。
local function video_ok()
	local w = mp.get_property_number("video-params/w")
	local h = mp.get_property_number("video-params/h")
	if not w or not h then
		return nil
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
	-- The vf list is updated asynchronously, so a second sync before the first
	-- attach has landed still reports has=false and appends another @vsr. Two
	-- VSR filters stacked = 4x instead of 2x, and each landing flips the chain
	-- again, which is what made the video尺寸 oscillate between 640x480 and
	-- 1280x960 several times a second. Remember the pending attach instead.
	if has then
		vsr_issued = false
	end

	-- 窗口拖动缩放期间（App 发布 user-data/mpvw/window-resizing）摘掉滤镜：
	-- d3d11vpp 让每次链路重配置贵得多，拖动中的连续重配置正是卡顿来源；
	-- 拖动结束后会自动重新挂载。
	if window_resizing then
		if has then
			mp.commandv("vf", "remove", "@vsr")
			msg.verbose("窗口缩放中，已移除 @vsr")
		end
		updating = false
		return
	end

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
		updating = false
		return
	end

	-- 跳转期间不做任何决定：滤镜挂载/摘除本身就会触发链路重配置，
	-- seek 期间反复摘挂正是抖动源。保持现状即可。
	if seek_suspended then
		updating = false
		return
	end

	local ok = video_ok()
	if ok == true then
		if not has and not vsr_issued then
			-- use pre so VSR always runs BEFORE @hdr (upscale first, then RTX HDR conversion)
			mp.commandv("vf", "pre", "@vsr:d3d11vpp=format=nv12:scale=" .. user_opt.scale .. ":scaling-mode=nvidia")
			vsr_issued = true
			msg.verbose("自动挂载 @vsr")
		end
	elseif ok == false then
		if has then
			mp.commandv("vf", "remove", "@vsr")
			msg.verbose("分辨率/轨道不满足条件，已移除 @vsr")
		end
	end
	-- ok == nil（链路正在重配置、参数暂时读不到）：保持现状，不做任何动作。

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
mp.observe_property("user-data/mpvw/window-resizing", "native", function(_, val)
	-- 兼容 boolean 与字符串两种形态
	window_resizing = (val == true) or (val == "yes")
	sync_vsr()
end)
mp.observe_property("user-data/mpvw/vsr-auto", "native", function(_, val)
	-- observe_property 注册时会立刻以当前值回调一次。App 尚未下发设置时该值为
	-- nil，若把它当作 "no" 就会覆盖掉上面的 enabled=true 默认（脚本本该默认
	-- 自动挂载 @vsr）。只有真正拿到值时才改变状态。
	if val == nil then
		return
	end
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
