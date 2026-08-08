--[[
hdr_auto.lua — 本地自定义（无上游）

RTX Video HDR 滤镜自动开关：
  1. 仅当【当前片源是 SDR】时挂载 @hdr:d3d11vpp=nvidia-true-hdr：
       auto：屏幕输出为 HDR（video-target-params.gamma == pq/hlg）时启用；
       on  ：强制启用（忽略屏幕检测，仍要求 SDR 片源）。
     HDR 片源绝不套该滤镜——RTX HDR 只做 SDR→HDR，对 HDR 片源二次转换会
     导致色彩异常高饱和（mpv issue #17800；本机 mpv 未包含 PR #18199 修复）。
  2. 滤镜生效期间临时设置 inverse-tone-mapping=no、target-colorspace-hint=yes
     （WinUI composition 模式下 mpv 拿不到显示器信息，hint=auto 会使目标色域失效、
       交换链退回 sRGB，导致“检测到 HDR 但画面发白”；本机固定 yes，移除后恢复原值）。
  3. 右键菜单 / script-message 手动覆盖：
       script-message-to hdr_auto mode auto   # 自动检测（默认）
       script-message-to hdr_auto mode on     # 强制开启（仅 SDR 片源）
       script-message-to hdr_auto mode off    # 强制关闭
  4. 当前模式写入 user-data/hdr-auto/mode 供菜单勾选显示。
  注意：RTX Video HDR 需要系统已开启 HDR（Win11 自动 HDR）；与 VSR 兼容，
        vsr_auto 用 vf pre 保证 VSR 在 HDR 之前（先超分再转 HDR）。
]]

local msg = require("mp.msg")

local user_opt = {
	load = true,
	mode = "auto", -- auto|on|off
	log = false,   -- 诊断日志（hdr_auto.log），默认关闭；script-opts/hdr_auto.conf 可开
}
mp.options = require("mp.options")
mp.options.read_options(user_opt)

if user_opt.load == false then
	msg.info("脚本已被初始化禁用")
	return
end

-- ===== OSD 提示本地化（跟随 App 界面语言） =====
local hdr_i18n = {
	["en-US"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR: HDR source skipped automatically (avoid double conversion)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR auto: display detected: ",
		["未知"] = "Unknown",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR: auto (display detected: %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR: force on (current display is not HDR, output may look wrong)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR: force on (SDR source only)",
		["RTX HDR：关闭"] = "RTX HDR: off",
	},
	["ja-JP"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR：HDRソースは自動スキップ（二重変換を回避）",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR 自動：画面検出 ",
		["未知"] = "不明",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR：自動（画面検出 %s）",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR：強制オン（現在の画面はHDRではありません。表示が乱れる可能性があります）",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR：強制オン（SDRソースのみ）",
		["RTX HDR：关闭"] = "RTX HDR：オフ",
	},
	["ko-KR"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR: HDR 소스 자동 건너뜀(이중 변환 방지)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR 자동: 화면 감지 ",
		["未知"] = "알 수 없음",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR: 자동(화면 감지: %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR: 강제 켜기(현재 화면이 HDR이 아님. 출력이 비정상일 수 있음)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR: 강제 켜기(SDR 소스만)",
		["RTX HDR：关闭"] = "RTX HDR: 끄기",
	},
	["de-DE"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR: HDR-Quelle automatisch übersprungen (Doppelkonvertierung vermeiden)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR automatisch: Anzeige erkannt: ",
		["未知"] = "Unbekannt",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR: automatisch (Anzeige erkannt: %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR: erzwingen (Anzeige ist nicht HDR, Ausgabe kann fehlerhaft sein)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR: erzwingen (nur SDR-Quelle)",
		["RTX HDR：关闭"] = "RTX HDR: aus",
	},
	["fr-FR"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR : source HDR ignorée automatiquement (éviter la double conversion)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR auto : écran détecté : ",
		["未知"] = "Inconnu",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR : auto (écran détecté : %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR : forcer (l'écran n'est pas HDR, l'affichage peut être anormal)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR : forcer (source SDR uniquement)",
		["RTX HDR：关闭"] = "RTX HDR : désactivé",
	},
	["es-ES"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR: fuente HDR omitida automáticamente (evita doble conversión)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR auto: pantalla detectada: ",
		["未知"] = "Desconocido",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR: automático (pantalla detectada: %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR: forzar (la pantalla no es HDR, puede verse mal)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR: forzar (solo fuente SDR)",
		["RTX HDR：关闭"] = "RTX HDR: apagado",
	},
	["ru-RU"] = {
		["RTX HDR：HDR 片源自动跳过（避免二次转换）"] = "RTX HDR: HDR-источник автоматически пропущен (избегаем двойного преобразования)",
		["RTX HDR 自动：屏幕检测 "] = "RTX HDR авто: обнаружен экран: ",
		["未知"] = "Неизвестно",
		["RTX HDR：自动（屏幕检测 %s）"] = "RTX HDR: авто (обнаружен экран: %s)",
		["RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"] = "RTX HDR: принудительно (экран не HDR, вывод может быть некорректным)",
		["RTX HDR：强制开启（仅 SDR 片源）"] = "RTX HDR: принудительно (только SDR-источник)",
		["RTX HDR：关闭"] = "RTX HDR: выкл",
	},
}

local function _(s)
	local t = hdr_i18n[mp.get_property_native("user-data/mpvw/language") or "en-US"]
	if t and t[s] then
		return t[s]
	end
	return s
end

local mode = user_opt.mode
if mode ~= "auto" and mode ~= "on" and mode ~= "off" then
	mode = "auto"
end

local updating = false
local saved_opts = nil
local hdr_source_notified = false
local seek_suspended = false
local last_diag = ""

local function diag(...)
	if not user_opt.log then
		return
	end
	local dir = mp.get_property("config-dir") or ""
	local f = io.open(dir .. "/hdr_auto.log", "a")
	if f then
		f:write(os.date("%H:%M:%S"), " ", table.concat({ ... }, " "), "\n")
		f:close()
	end
end

local function vf_list()
	return mp.get_property_native("vf") or {}
end

local function has_hdr(vf)
	for _, f in ipairs(vf) do
		if f.label == "hdr" then
			return true
		end
	end
	return false
end

-- 当前显示目标是否处于 HDR 输出（HDR10 显示器 + Windows HDR 开启时 gamma=pq）
local function display_hdr()
	local tp = mp.get_property_native("video-target-params")
	if tp and tp.gamma then
		local g = tostring(tp.gamma):lower()
		if g == "pq" or g == "st2084" or g == "hlg" then
			return true
		end
		-- composition 模式下 gamma 常与输出配置不同步（srgb/unknown），
		-- 只有明确为 SDR 目标才判否，其余情况回退到 App 的显示器检测
		if g == "srgb" or g == "bt.709" or g == "709" then
			return false
		end
	end
	-- WinUI composition 渲染模式下拿不到 video-target-params，
	-- 回退到 App 写入的 user-data/mpvw/color-kind（DisplayColorKind.HDR）。
	return mp.get_property("user-data/mpvw/color-kind") == "HDR"
end

-- 当前片源是否为 HDR（与 profiles.conf 的 HDR_generic 判定一致）
-- 返回 nil=参数未知（不挂载），false=明确 SDR，true=HDR
local function source_is_hdr()
	local known = false
	local hdr = false
	local gamma = mp.get_property("video-params/gamma")
	if gamma then
		known = true
		local g = gamma:lower()
		if g == "pq" or g == "st2084" or g == "hlg" then
			hdr = true
		end
	end
	local peak = mp.get_property_number("video-params/sig-peak")
	if peak then
		known = true
		if peak > 1.001 then
			hdr = true
		end
	end
	local maxluma = mp.get_property_number("video-params/max-luma")
	if maxluma then
		known = true
		if maxluma > 203 then
			hdr = true
		end
	end
	if not known then
		return nil
	end
	return hdr
end

local function set_mode_prop()
	mp.set_property("user-data/hdr-auto/mode", mode)
end

-- RTX HDR 生效期间需要配合的选项（issue #17800 的验证配置），退出时恢复
local function apply_hdr_opts(on)
	if on then
		if not saved_opts then
			saved_opts = {
				inverse = mp.get_property("inverse-tone-mapping"),
				hint = mp.get_property("target-colorspace-hint"),
			}
			mp.set_property("inverse-tone-mapping", "no")
			mp.set_property("target-colorspace-hint", "yes")
		end
	elseif saved_opts then
		if saved_opts.inverse ~= nil then
			mp.set_property("inverse-tone-mapping", saved_opts.inverse)
		end
		if saved_opts.hint ~= nil then
			mp.set_property("target-colorspace-hint", saved_opts.hint)
		end
		saved_opts = nil
	end
end

local function sync_hdr()
	if updating then
		return
	end
	updating = true

	local vf = vf_list()
	local has = has_hdr(vf)
	local video_no = mp.get_property("video") == "no"
	local src_hdr = source_is_hdr()
	local want = not video_no and not seek_suspended and src_hdr == false and
		(mode == "on" or (mode == "auto" and display_hdr()))
	local diag_line = "sync mode=" .. tostring(mode) ..
		" kind=" .. tostring(mp.get_property("user-data/mpvw/color-kind")) ..
		" src=" .. tostring(src_hdr) .. " want=" .. tostring(want) .. " has=" .. tostring(has) ..
		" dh=" .. tostring(display_hdr())
	if diag_line ~= last_diag then
		diag(diag_line)
		last_diag = diag_line
	end

	if want and not has then
		mp.commandv("vf", "append", "@hdr:d3d11vpp=nvidia-true-hdr")
		diag("vf", "appended @hdr")
		apply_hdr_opts(true)
		msg.info("SDR 片源" .. (display_hdr() and "，屏幕支持 HDR" or "（强制模式）") ..
			"，已挂载 RTX Video HDR 滤镜")
	elseif not want and has then
		mp.commandv("vf", "remove", "@hdr")
		diag("vf", "removed @hdr")
		if not seek_suspended then
			apply_hdr_opts(false)
		end
		local reason
		if seek_suspended then
			reason = "跳转/拖拽期间临时摘除"
		elseif src_hdr == true then
			reason = "HDR 片源无需 RTX HDR"
		elseif src_hdr == nil then
			reason = "片源信息未就绪"
		else
			reason = "屏幕不支持 HDR 或已关闭"
		end
		msg.info(reason .. "，已移除 RTX Video HDR 滤镜")
	elseif want and has then
		apply_hdr_opts(true)
	end

	-- HDR 片源跳过提示（每个文件只提示一次）
	if src_hdr == true and mode ~= "off" and not hdr_source_notified then
		hdr_source_notified = true
		mp.commandv("show-text", _("RTX HDR：HDR 片源自动跳过（避免二次转换）"), 2000)
	elseif src_hdr ~= true then
		hdr_source_notified = false
	end

	updating = false
end

mp.observe_property("video-target-params", "native", sync_hdr)
mp.observe_property("user-data/mpvw/color-kind", "native", function(_, val)
	if mode == "auto" then
		mp.commandv("show-text", _("RTX HDR 自动：屏幕检测 ") .. tostring(val), 1500)
	end
	sync_hdr()
end)
mp.observe_property("video-params", "native", sync_hdr)
mp.observe_property("vf", "native", sync_hdr)
mp.register_event("file-loaded", sync_hdr)

-- 跳转/拖拽期间临时摘掉 @hdr：精确 seek 不再被 RTX HDR 拖慢，
-- 拖拽（ModernX 会暂停播放）期间保持摘除，恢复播放时挂回。
mp.observe_property("seeking", "native", function(_, val)
	if val == true then
		if not seek_suspended then
			seek_suspended = true
			sync_hdr()
		end
	elseif seek_suspended and mp.get_property("pause") ~= "yes" then
		seek_suspended = false
		sync_hdr()
	end
end)
mp.observe_property("pause", "native", function(_, val)
	if val == false and seek_suspended then
		seek_suspended = false
		sync_hdr()
	end
end)

mp.register_script_message("mode", function(new_mode)
	if new_mode ~= "auto" and new_mode ~= "on" and new_mode ~= "off" then
		msg.warn("未知模式: " .. tostring(new_mode))
		return
	end
	mode = new_mode
	set_mode_prop()
	sync_hdr()
	if mode == "auto" then
		local kind = mp.get_property("user-data/mpvw/color-kind") or _("未知")
		mp.commandv("show-text", string.format(_("RTX HDR：自动（屏幕检测 %s）"), tostring(kind)), 2000)
	elseif mode == "on" then
		if not display_hdr() then
			mp.commandv("show-text", _("RTX HDR：强制开启（当前屏幕非 HDR，可能显示异常）"), 2500)
		else
			mp.commandv("show-text", _("RTX HDR：强制开启（仅 SDR 片源）"), 1500)
		end
	else
		mp.commandv("show-text", _("RTX HDR：关闭"), 1500)
	end
end)

set_mode_prop()
diag("loaded", "mode=" .. tostring(mode),
	"kind=" .. tostring(mp.get_property("user-data/mpvw/color-kind")))
