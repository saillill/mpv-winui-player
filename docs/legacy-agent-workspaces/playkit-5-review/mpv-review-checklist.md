# mpv 自定义配置层 · 完整改动清单与审查清单

> 用途：交给一个新的 Codex 对话做全量审查。审查前先读完本文件、README.md 与 HANDOFF.md；
> 审查过程只读优先，发现问题先列报告，修改需用户确认。

## 0. 项目位置

| 项目 | 路径 |
| --- | --- |
| 配置/脚本 Git 仓库 | `C:\Users\z2637\Documents\Codex\2026-08-04\mpv-lazy-hooke007-mpv-playkit-codex\work\mpv-config`（origin=github.com/saillill/mpv-config，main） |
| 安装目录（实测环境） | `C:\Program Files\mpv-lazy\portable_config` |
| 上游 mpv-lazy | `C:\Program Files\mpv-lazy`（mpv v0.41.0-615-g7b057f66f） |
| 交接文档 | `...\work\HANDOFF.md`（本文件摘要 + 历史决策） |
| 编译工具 | `...\work\tools\w64devkit`（gcc：`x86_64-w64-mingw32-gcc.exe`） |
| menu.dll 源码（本地重编译） | `...\work\menu-plugin\`（含 mpv 头 `include\mpv\`、构建脚本 `build_menu_dll.ps1`） |
| darkmode.dll 源码 | `...\work\tools\darkmode.c` |
| 上游参照 | `...\work\upstream\`（ModernZ v0.3.3、mpv commit 7b057f66f 的 console/select、各脚本原版） |

## 1. 整体架构（当前状态）

- 菜单统一走 **menu.dll（Win32 原生弹窗）**：右键、播放列表按钮、音轨按钮。
  `dyn_menu.conf: use_mpv_impl=no`；`menu.dll` 是本地重编译版（见第 5 节）。
- 主题**跟随 Windows 深浅色**：darkmode.dll 在窗口线程按 `AppsUseLightTheme` 应用
  `SetPreferredAppMode(AllowDark/AllowLight)`；console.lua 用 LuaJIT FFI 直读同一注册表项切黑白配色。
- 其余基础自定义（ModernZ UI、HDR/VSR、seek_hold、OSD 字号、缩放 profiles）保持不变。

## 2. Git 状态

- 基线 commit：`e0ffda7`（上一轮工作结束点）。
- 当前共 11 个本地提交（**均未推送**，按时间顺序）：

| Commit | 内容 |
| --- | --- |
| `05199b8` | 菜单统一走 mpv 原生 context-menu；播放列表按钮临时替换 menu-data 弹原生菜单 |
| `cd33201` | 音频菜单统一原生 + dyn_menu 动态条目中文化；audio-menu 消息 |
| `e3babff` | 主题自动切换（darkmode 写属性 + console 黑白配色）；OSD 宽度 40%→30%；标题截断 50 |
| `9c52dad` | 修复带窗口启动崩溃（darkmode 窗口线程不再调 mpv API）；console 改用 FFI 读注册表 |
| `b606fe3` | 修复宽度缓存：本地重编译 menu.dll（每次销毁重建 HMENU）；切回 use_mpv_impl=no |
| `8803a72` | 修复按钮菜单竞态：menu.dll 新增独立临时通道 temp-items + show-temp |
| `0ddd28c` | 修复菜单完全失效：恢复误删的 show_button_menu；show-temp 同步读属性建菜单 |
| `fc3fef5` | 修复按钮菜单不显示：show-temp 走窗口线程（WM_SHOWTEMP）；播放列表按钮右键改 ignore |
| `2203f83` | 修复 console 菜单几何：条目与高亮对齐、标题入框、分隔线框内宽、命中区对齐；每次 render 刷新调色板（深浅色热切换） |
| `9a44069` | 审查修复：darkmode 去调试日志重编译、dynamic_menu 禁用标签、seek_hold 注释、modernz.conf/profiles/README/HANDOFF 修正 |
| `288430e` | console 菜单条目锚点统一为 an(4) 居中于槽位（与上游一致），历史搜索模式文字不再上移 |

- `git status` 干净；`e0ffda7..HEAD` 共改 8 个文件，本轮审查再改 7 个：README.md、script-opts/dyn_menu.conf、
  scripts/console.lua、scripts/darkmode.dll、scripts/dyn_menu.lua、scripts/menu.dll、
  scripts/modernz.lua、scripts/select.lua、scripts/seek_hold.lua、scripts/dynamic_menu.lua、
  profiles.conf、script-opts/modernz.conf。
- 安装目录与仓库**哈希一致**（menu.dll / dyn_menu.lua / dyn_menu.conf / modernz.lua / select.lua / console.lua / darkmode.dll）。

## 3. 文件级改动清单（审查核对用）

### 3.1 本轮改动（e0ffda7 之后）

| 文件 | 改动点 | 审查要点 |
| --- | --- | --- |
| `script-opts/dyn_menu.conf` | `use_mpv_impl=no`；`max_title_length=50` | 注释应说明本地 menu.dll 宽度修复 |
| `scripts/menu.dll`（二进制，99328B） | 本地重编译 | 源码/构建见第 5 节；不要与上游 489472B 版本混用 |
| `scripts/dyn_menu.lua` | 新增 playlist-menu/audio-menu；中文动态条目；标题截断；show_button_menu 恢复 | 见第 6 节静态审查项 |
| `scripts/modernz.lua` | 播放列表按钮左键→playlist-menu；音轨按钮左键→audio-menu；播放列表按钮右键→ignore | 其余为历史改动（标题右侧、showonselect） |
| `scripts/select.lua` | 「播放列表」「音轨」条目指向原生菜单 | 其余为中文翻译版 |
| `scripts/console.lua` | FFI 读注册表主题；dark/light 双调色板；宽度 0.3 | 无固定深色残留；无 mpv API 从错误线程调用 |
| `scripts/darkmode.dll`（本轮重编译，12800B） | 自动 AllowDark/AllowLight | 源码 darkmode.c；窗口线程只调系统 API；已去除调试日志 |
| `scripts/console.lua`（本轮修复） | 菜单几何 + 主题热切换 | 条目/标题/分隔线/命中区锚点一致；render 时重刷调色板 |
| `scripts/dynamic_menu.lua`（本轮修复） | 「锐化强度调节」由带标题 separator 改为禁用标签 | build_json 透传 state |
| `README.md` | 登记全部本地补丁 | 本地脚本必须登记 |

### 3.2 历史改动（e0ffda7 之前，已稳定）

| 文件/项 | 内容 |
| --- | --- |
| ModernZ | `layout=default` 居中；标题/章节名右侧（an3 + clip）；showonselect（菜单开关时进度条不消失）；vidscale=auto；1080 基准 1.0；osc_height 48；蓝色主题 #1D96F5；拖拽=精确 seek（0.1s 节流）+松手精确落点 |
| profiles.conf | ui_scale_1440p / ui_scale_4k / portrait_ui 系列（0.65）；HDR_generic；av1_hwdec；speed_limit；vsync_auto 等 |
| mpv.conf | osc=no、load-console=no、load-select=no；osd 字号 20/描边 0.8；autocreate-playlist；hwdec；截图等 |
| script-opts.conf | console-* 覆盖（font_size=12 等）；stats-*；save_global_props；MediaInfo 路径 |
| console.conf | 字号 12/描边 0.6/gap 0.06/padding 6/圆角 6（颜色由 console.lua 控制） |
| stats.conf / stats_mediainfo.conf | 15/0.8；22/16/12/10 |
| thumbfast.conf | precise=0、frequency=0.15、max_height=2000 |
| hdr_auto.lua / vsr_auto.lua | 本地脚本：SDR+屏幕 HDR 才挂 RTX HDR；HDR 片源跳过；VSR 2x 且 vf pre；seek 期间摘除 |
| seek_hold.lua | seek/暂停期间 auto-window-resize=no，1.5s 后还原 |
| dynamic_menu.lua | 本地脚本：VSR/HDR/RIFE/着色器动态菜单数据 |
| input.conf | 全中文 #menu: 定义；方向键 seek 5 exact；Del=modernz/visibility 等 |

### 3.3 未改动的上游脚本（不要误判为本地改动）

`auto_sub_fonts_dir.lua`、`coverart.lua`、`metadata_osd.lua`、`recentmenu.lua`、
`thumbfast.lua`（mpv-lazy 适配版）、`k7f_zen.lua`、`stats.lua`（适配版）、
`stats_mediainfo.lua`、`input_plus.lua`、`dialog.lua` —— 均来自上游/mpv-lazy，无需审查代码，
只需确认 README 登记与许可证保留。注意：`k7f_zen.lua` 仅有一处注释补丁（“实测兼容至 1.1.1”），
无功能差异；前述 5 个脚本与 mpv-lazy 20260510 发行原版哈希一致。

## 4. 用户规范（审查是否符合）

1. 键位冲突由 Codex 分配；
2. 冗余删除，只保留与上游相关；本地自定义脚本在 README 登记；
3. UI 按实际分辨率缩放（1080=1.0、4K 放大、窗口缩放跟随、竖屏自适应）；
4. HDR 与补帧可共存；HDR 滤镜与 VSR 可共存；屏幕不支持 HDR 不挂；HDR 片源绝不套 RTX HDR；
5. GitHub 打包：除观看历史外全部上传（含 mpv 本体、运行库、umpv、注册/测试脚本），解压即用；
6. 菜单规范：WinUI 风格、跟随系统深浅色、左下贴左留空隙、不透明、字号小（12）、
   窗口细（30% 宽上限）、标题下分隔线、隐藏输入条、小圆角（6）；
7. 进度条在菜单打开/取消/鼠标停留时不消失；
8. 标题（视频名）在控制栏右侧；
9. 右键菜单跟随系统深浅色；
10. 1080 窗口 UI 基准 1.0。

## 5. 本地重编译二进制（如何复现）

### menu.dll
- 源码：`work\menu-plugin\`（mpv-menu-plugin main 分支 + 3 处本地补丁）
- 补丁 1（menu.c update_menu）：每次更新 `DestroyMenu` + `CreatePopupMenu` 重建 HMENU，
  修复 Win32 菜单宽度首次显示后被缓存的问题；
- 补丁 2（plugin.c/menu.c/menu.h/plugin.h）：独立临时通道
  `user-data/menu/temp-items` + `show-temp` 消息；show 时**同步**读属性建菜单；
- 补丁 3（plugin.c）：`show-temp` 用 `PostMessage(WM_SHOWTEMP)` 到窗口线程，
  在 WndProc 里 `TrackPopupMenuEx(TPM_RETURNCMD)` 并执行选中命令。
- 构建：`work\menu-plugin\build_menu_dll.ps1`（w64devkit + `MPV_CPLUGIN_DYNAMIC_SYM`，
  无需链接 mpv 导入库；输出 menu-built.dll）。PE 时间戳不同导致每次哈希不同属正常。

### darkmode.dll
- 源码：`work\tools\darkmode.c`
- 行为：读 `HKCU\...\Themes\Personalize\AppsUseLightTheme`，深色→`SetPreferredAppMode(2)`，
  浅色→`(1)`；枚举并子类化 mpv 窗口，在窗口线程处理消息时应用；
  **窗口线程严禁调用 mpv API**（曾导致带窗口启动 0xC0000005 崩溃）。
- 构建：`x86_64-w64-mingw32-gcc -shared -O2 -s -o darkmode.dll darkmode.c`（PATH 需含 w64devkit\bin）。

## 6. 静态审查清单（逐项打勾）

- [ ] `dyn_menu.lua`：`show_button_menu` 函数存在且右键绑定调用它（曾误删导致右键失效）；
- [ ] `dyn_menu.lua`：playlist-menu/audio-menu 在 `use_mpv_impl=no` 时走 temp-items + show-temp，
  在 `=yes` 时走 menu-data + context-menu 回退；无 0.5s 还原定时器残留（temp 通道不需要还原）；
- [ ] `dyn_menu.lua`：动态条目无英文残留（音轨/关闭/自动/自动选择设备/默认/强制/外部/章节/版本/条目）；
- [ ] `menu.c`：无调试日志（`menu_log`/fprintf 到 %TEMP%）；update_menu 重建 HMENU；
  show_temp_menu 同步 `mpv_get_property`；
- [ ] `plugin.c`：WM_SHOWTEMP 存在；temp 属性不再 observe（避免与同步读取冲突）；
- [ ] `darkmode.c`：无 `mpv_set_property` / GetProcAddress("mpv_*")；无 %TEMP% 调试日志；
  DllMain 无冗余调用；
- [ ] `console.lua`：FFI 代码在 pcall 内；palettes.dark/light 键完整；宽度上限 0.3；
  无 `user-data/windows/theme` observe 残留（已改 FFI 直读）；
- [ ] `console.lua` 菜单模式几何：条目用 an(alignment) 与高亮行严格对齐；标题在框内；
  分隔线 rect_cw(-padding, …, max_item_width+padding, …)；命中区 item_x0 = padding+8-padding = 8；
  render 顶部调用 apply_palette（深浅色运行中切换生效）；
- [ ] 配置文件 UTF-8 无 BOM；Lua 文件与 installed 一致（哈希）；
- [ ] 仓库无 `recent.json`、`_cache/`、临时文件、*.log；
- [ ] 安装目录与仓库**全量递归哈希一致**（排除 recent.json/_cache/saved-props.json 运行时文件）；
- [ ] README 已登记：playlist-menu/audio-menu、菜单主题、宽度、darkmode.dll、menu.dll 重编译；
  组件表逐行无乱码/无陈旧描述（select.lua/console.lua 中文定制版、darkmode 跟随深浅色）；
- [ ] 无重复/失效脚本（如旧 ModernX、多余备份 .bak）。
- [ ] input.conf 引用的 script-binding/script-message 均在对应脚本注册
  （input_plus：vf_hold/glsl_hold/af_hold/speed_auto/seek_acc/import_url/pip_dummy_pct；
  stats_mediainfo：display_toggle/copy2clipboard；save_global_props：clean_data；
  dialog：open/open-folder/open-clipboard/save/set-clipboard；modernz：visibility；recentmenu：open）。
- [ ] `dynamic_menu.lua`：带标题的菜单项为禁用标签（state={"disabled"}），不是 separator；

## 7. 行为验收清单（实机，逐项打勾）

> 每次改动后必须**完全退出 mpv 再重开**（旧进程会锁 DLL/加载旧脚本）。
> 2026-08-06 已用自动探针验证：vsr_auto 共存逻辑（加其它滤镜摘 @vsr、移除后恢复）、
> hdr_auto 强制模式 SDR 挂载稳定无振荡（4s 采样 vf 不变）、HDR10 片源强制模式下仍不挂 @hdr、
> 全脚本加载无错误、darkmode 不再写日志、select 总菜单开/关正常（console-open 属性）、
> playlist-menu/audio-menu 临时通道数据正确、seek_hold 的 auto-window-resize 切换/恢复正常。
> OSD 几何已通过 ASS 导出 + libass 实渲染像素验证（框/标题/条目/高亮/分隔线/命中区全部对齐）。
> 注意：本 mpv 构建的 screenshot window/subtitles 均不合成 OSD，OSD 视觉验收需真实窗口目视。

- [ ] 启动无崩溃（有窗口）；
- [ ] 任意位置右键 → 完整原生菜单（打开/播放/导航/轨道/…/退出）；
- [ ] 播放列表按钮左键 → 播放列表原生菜单，当前项勾选；
- [ ] 音轨按钮左键 → 音轨原生菜单（音轨 1…/关闭），当前项勾选；
- [ ] 播放列表按钮右键 → 只弹完整菜单，**无叠加的 OSD 设置界面**；
- [ ] 宽度独立：先开音轨菜单再开播放列表/右键，各菜单宽度按各自内容计算；
- [ ] 深浅色：Windows 设置切浅色/深色，原生菜单与 select 菜单跟随切换（浅色=白底黑字）；
- [ ] **运行中**切换 Windows 深浅色，OSD 菜单颜色立即跟随（不重启 mpv）；
- [ ] OSD 菜单几何：标题在框内、条目文字与高亮条完全对齐、分隔线横贯框内宽、
  鼠标命中区域与可见菜单一致（左下框左缘 ≈8 单位）；
- [ ] 点击菜单项执行正确：播放列表切换、音轨切换、输出设备切换；
- [ ] select 总菜单仍可进：右键菜单 > 工具 > 打开select总菜单；
- [ ] 文件对话框：右键菜单 > 打开 > 文件…/文件夹… 正常；
- [ ] HDR/VSR：SDR+屏幕 HDR 挂 RTX HDR、HDR 片源跳过、VSR 2x、二者共存、seek 提速；
- [ ] RIFE 补帧与 HDR/VSR 共存：挂 @rife 时 vsr_auto 摘 @vsr，移除 RIFE 后自动恢复；
- [ ] seek_hold：拖进度条/seek 期间窗口不跳大小；
- [ ] 缩放：1080p 基准、4K/1440p/竖屏 profiles 生效；
- [ ] 进度条：菜单打开/取消/鼠标停留时不消失；
- [ ] 标题（视频名）在控制栏右侧。

## 8. 已知坑（新对话必须遵守）

- 替换 DLL 前必须无 mpv 进程（文件被锁）；
- `script-opts.conf` 的 `console-*` 覆盖优先级高于 `script-opts/console.conf`，改字号/字体两处同步；
- Win32 弹窗宽度按 HMENU 缓存：复用句柄改条目不重新测量 → 必须重建 HMENU（menu.dll 已处理；
  mpv 内置 context-menu 无解，所以不用 `use_mpv_impl=yes`）；
- Win32 弹窗必须在窗口线程 `TrackPopupMenuEx`（插件线程直接弹窗不显示）；
- darkmode 窗口线程只调系统 API；
- console.lua/select.lua 必须匹配 mpv build commit `7b057f66f`，升级 mpv 后重新核对；
- ASS 绘图：坐标原点就是 `\pos`；`\p4` 坐标渲染时 ÷8；
- mpv Lua `keypress` 命令无法模拟鼠标键（测试右键需 SendInput 或真实点击）；
- FFI 把数字当句柄（如 HKEY_CURRENT_USER）必须 `ffi.cast`，直接传数字报错；
- 菜单数据 `user-data/menu/items` 是共享数据，任何动态更新都会覆盖 → 按钮专用菜单必须走
  `user-data/menu/temp-items` 独立通道。

## 9. 审查输出建议

新对话完成后输出一份报告，至少包含：
1. 合规项（逐条对照第 4、6、7 节）；
2. 问题项（文件+行号+影响+建议）；
3. 待办确认：push 11 个本地提交（上一轮 8 + 审查修复 3）。Release 完整包已本地重打并验证：
   `work\mpv-lazy-portable-20260806.7z.001/.002`（SHA256 见 `work\SHA256SUMS-20260806.txt`），
   上传与发布不着急（用户确认），按用户安排进行。原 7z 归档经核查严重过期
   （ModernX 旧 UI + 上游旧 menu.dll/dyn_menu + 缺 console/select/modernz/darkmode/seek_hold + 旧 hdr/vsr），
   不可再作为发布物；
4. 需要用户拍板的决策点（如有）。
