# MpvMenuHost —— 单进程 WinUI 3 菜单宿主

独立 C# WinUI 3 进程，替代 menu.dll 的 HMENU 弹窗：菜单弹窗 → IPC → mpv 执行命令。
根菜单与全部级联子菜单由 WinUI 官方 `MenuFlyout` 在**同一个宿主进程**内渲染
（宿主窗口 1×1 透明置顶，仅作 Flyout 放置目标）；不再有每级进程、不再有
trigger/heartbeat/state/RESET 文件预热协议。

## 构建

```powershell
dotnet build -c Debug -p:Platform=x64
```

产物：`bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MpvMenuHost.exe`
（Windows App SDK 自包含，无需系统级 WAS 运行时）。

## 命令行

```
MpvMenuHost.exe --items <menu.json> [--pipe <name|\\\\.\\pipe\\name>] [--log <file>]
                [--test-select N] [--test-delay-ms N] [--no-connect]
                [--theme dark|light] [--pos X,Y]
                [--anchor top|bottom] [--side left|right] [--depth N]
```

- `--items`：菜单 JSON（缺省显示内置示例菜单）。
- `--pipe`：mpv `--input-ipc-server` 的管道名；点击菜单项时连接并发送命令。
- `--log`：调试日志（测试断言用；正式使用不传；Lua 侧由 dyn_menu.conf 的
  `host_log` 传入）。
- `--test-select N`：显示后自动点击第 N 个可用项（M2 无头验证钩子）。
- `--no-connect`：不发 IPC，仅测试 UI。
- `--theme dark|light`：强制主题（仅测试/预览用；缺省跟随 Windows 深浅色，
  运行中 WM_SETTINGCHANGE 热切换被跳过）。
- `--pos X,Y`：固定弹窗位置（测试用）。
- `--anchor top|bottom`：菜单锚点（top=菜单左上角在鼠标处；bottom=底边在鼠标
  上方，播放列表/音轨按钮菜单用）。
- `--side left|right`：级联展开方向（继承上层；当前 MenuFlyout 平台自动处理，
  参数保留兼容）。
- `--depth N`：菜单深度（根=0，保留用于日志/扩展）。

> v4.0 起子菜单为同进程渲染：`--prewarm / --prewarm-id / --parent-pid /
> --parent-prewarm-id` 已删除，不再需要。

## 菜单 JSON

```json
{
  "title": "播放列表",
  "items": [
    { "title": "播放列表", "state": ["disabled"] },
    { "type": "separator" },
    { "title": "001.mkv", "shortcut": "MKV",
      "cmd": ["playlist-play-index", 0], "state": ["checked"] },
    { "title": "暂停", "cmd": "cycle pause" }
  ]
}
```

`cmd` 为数组（推荐）或字符串（自动按空格拆分，尊重双引号）。
`submenu` 可任意嵌套（v4.2 起递归解析，不再限 3 层）。

## 行为（v4.1 现状）

- 渲染：根菜单与全部子菜单由 WinUI 官方 `MenuFlyout` 提供（图标/级联/展开动画/
  Win11 圆角/亚克力/键盘导航/Esc/点击外部关闭均为平台内置）；
- 宿主窗口 1×1 透明置顶（窗口级 alpha=0 + 1×1 空区域），仅作 Flyout 放置目标；
  WS_EX_NOACTIVATE + TOOLWINDOW，不抢焦点、不进任务栏；不设 CS_DROPSHADOW；
- 子菜单悬停展开、与一级菜单重叠、展开动画等观感由 MenuFlyout 平台实现
  （Win11 原生观感）；
- 高度限幅与超长菜单滚动、屏幕边缘自动翻转/钳制由 Flyout 平台处理；
- 图标：系统预设 Segoe 字形（157 精确 + 47 前缀，静态条目全覆盖；Win10 自动回退
  Segoe MDL2 Assets）；用户可用 exe 同目录 `icon-map.json` 覆盖（见下）；
- 深/浅色跟随 Windows（未打包模式用元素级 RequestedTheme，不强制 Application 主题）；
- 快捷键文本只显示不注册真实加速键（`KeyboardAcceleratorTextOverride`，见 v4.1）；
- 点击菜单项 → 连 mpv IPC → 发送命令 → 退出；点外/Esc 关闭、↑/↓ 导航、Enter
  选中均由 MenuFlyout 平台处理。

## mpv 版本矩阵

| 依赖 | 最低要求 | 当前环境 |
|---|---|---|
| mpv | ≥0.37（menu.dll 上游要求）；菜单 JSON/IPC 依赖长期稳定 API | 0.41.0-615（mpv-lazy 构建） |
| menu.dll | tsl0922/mpv-menu-plugin（`host_menu=no` 回退路径） | 本地重编译版 |
| Lua | 5.1/5.2 兼容（setfenv/load 分支） | mpv 内置 |
| Windows | 10.0.17763+（net10.0-windows10.0.26100 目标 + 自包含 WAS） | Win11 本机 |

低于上表版本不保证可用；旧版 mpv 未实测。

## 本地化

- 静态菜单树由数据源（input.conf / 菜单 JSON）决定，宿主不翻译；
- 插件硬编码文本（轨道默认名、章节/版本/条目占位、Off/Auto、空列表提示、
  按钮菜单标题等）默认采用 **mpv 上游英文原文**，不再硬编码中文；
- 自定义经 `script-opts/dyn_menu.conf` 覆盖：`str_video=视频` 这类选项直接改
  可见文本（默认已附带一组中文覆盖；注释掉即恢复英文原版）。可用键：
  `str_video / str_audio / str_subtitle / str_channels / str_forced /
  str_external / str_default / str_off / str_auto / str_chapter / str_edition /
  str_edition_default / str_auto_device / str_item / str_playlist_empty /
  str_playlist / str_no_audio`；
- 宿主无用户可见硬编码文案（示例菜单为英文、仅测试用）；
- 若需整树英文，还需自行提供英文 input.conf（属于用户配置，非插件硬编码）。

## 外观与可配置性决策

- v4.1 起外观（主题/字体/图标/动画/圆角/行高）全部采用 WinUI 平台默认 + 跟随系统，
  不暴露外观参数（与“尽量依赖平台组件”原则一致）；
- `--theme dark|light` 仅测试/预览用；
- 图标支持用户覆盖：exe 同目录 `icon-map.json`（缺省不启用）：

  ```json
  {
    "exact": { "自定义标题": "\uE768" },
    "prefix": [ ["自定义前缀", "\uE721"] ]
  }
  ```

  用户精确匹配优先于内置精确，用户前缀优先于内置前缀；文件缺失/损坏时忽略并
  继续使用内置映射。码点需当前字体（Segoe Fluent Icons / Segoe MDL2 Assets）支持。

## v4.1（2026-08-07，MenuFlyout 渲染替换）

- 自绘渲染层整块移除：自定义行/悬停/级联窗口/定位/重叠/动画/低层钩子/置顶循环
  （MainWindow 1310 → 约 330 行，新增 IconMap.cs 独立图标映射）；
- 根菜单与全部子菜单改为 WinUI 官方 `MenuFlyout`：图标（FontIcon/Segoe）、
  子菜单（MenuFlyoutSubItem）、展开动画、Win11 圆角/亚克力、键盘/Esc/点外关闭
  全部平台内置，不再有“三级被一级覆盖”“多窗口闪烁”的 z-order 问题；
- 宿主窗口 1×1 透明置顶，仅作 Flyout 放置目标；点击叶子仍经 mpv JSON IPC 执行；
- 快捷键文本（如 Ctrl+1）只设置 `KeyboardAcceleratorTextOverride` 显示在行尾，
  不创建真实 KeyboardAccelerator：mpv 本体已处理快捷键；WinUI 解析非法键文本
  （Alt+-、·、TAB、Del 等）会在展开子菜单时卡死/崩溃（microsoft-ui-xaml #708），
  该做法与微软 Terminal 规避方案一致；
- 编译 0 警告 0 错误；未跑动态测试。

## v4.2（2026-08-07，审查修正）

- 菜单 JSON 递归解析：`MenuData.Load` 不再限制 3 层，任意深度子菜单完整加载
  （对应审查报告 C5/P1）；
- 图标用户覆盖：exe 同目录可选 `icon-map.json`（精确/前缀两级覆盖内置 157+47
  映射），缺省不启用、文件损坏静默忽略（对应审查报告 U5/P2）；
- 动态项图标：播放列表/轨道/配置文件/着色器等无图标叶子项继承所属子菜单图标，
  修复合集对齐（对应审查报告 F4/J4，P2）；
- 图标映射扩至 159 精确 + 52 前缀：补齐“脚本/滤镜”及 Nvidia/RTX/RIFE/K7/NVScaler
  动态子菜单，真实右键菜单样本 224/224 叶子项全部有图标；
- 菜单文本本地化：dyn_menu.lua 硬编码中文全部改为 mpv 上游英文原文（str_* 选项），
  自定义经 dyn_menu.conf 覆盖（默认附带中文覆盖组）；宿主示例菜单改英文；
- 参照 ikas-mc/mpv-winui-player 吸收三点：`&&`→`&` 标题还原、空子菜单跳过、
  连续分隔线去重（对应审查 F2 还原度）；
- README 按 v4.1 现状重写“行为”章节，补充 CLI 参数、mpv 版本矩阵、本地化与
  外观可配置性决策（对应审查报告 P1）；
- 编译 0 警告 0 错误，已部署（DLL `010F4646`，两侧一致）；未跑动态测试。

## v4.0（2026-08-07，单进程重构）

- 子菜单改为**进程内窗口**：`OpenSubmenu` 不再写子菜单 JSON、不再 spawn 进程，
  直接以内存 `MenuData` 创建同进程 WinUI 窗口（美术零改动，复用同一套渲染/动画/定位）；
- 删除每级进程链与文件预热协议：`--prewarm/--prewarm-id/--parent-pid/
  --parent-prewarm-id`、trigger/heartbeat/state/RESET、taskkill 清理全部移除；
- 低层鼠标/键盘钩子只由根窗口安装；外部点击按整条窗口链判定，键盘按最深级路由，
  Esc 关闭整链（与原进程模型行为一致）；
- 置顶：最末级保持 TOPMOST，父级每 100ms 自愈插到子窗口正下方
  （修复“三级打开时二级被一级覆盖”）；
- 关闭链：子窗口走 `Window.Close()`，仅根窗口 `Environment.Exit`；
- Lua 侧 `launch_host_menu` 直接冷启动单个宿主（无预热）；%TEMP% 旧协议残留已清零；
- 编译 0 警告 0 错误（Debug/Release），已部署 repo 与安装目录
  （DLL 哈希 `2BE157F9B993`，两侧一致）；**未跑动态测试**，实机观感待验证。

## 已知限制（原型期）

- select 总菜单入口（工具 > 打开select总菜单）仍为 OSD 菜单，未迁入宿主；
- 宿主自包含 WAS 分发体积约 266MB×2（repo + 安装目录各一份）；
- 动态项（播放列表/轨道名/设备名等）无图标（P2）；
- 双语模板与 locale 开关未提供（P2，见“本地化”）；
- 多显示器 DPI（PerMonitorV2）仅本机 100% 缩放验证过。

## 已验证（2026-08-06）

1. M1 UI：示例菜单渲染、窗口尺寸/位置/样式正确（浅色主题下白底黑字，跟随系统）；
   测试脚本 `test\run_m2_mock_test.ps1`。
2. M2 IPC（mock）：`test\mock_pipe_server.ps1` 收到
   `{"command":["playlist-play-index","0"],"request_id":1}`。
3. M2 IPC（真 mpv E2E）：`test\run_m2_mpv_test.ps1` 起 mpv
   `--input-ipc-server`，宿主发送 `set pause yes`，mpv 返回 success，
   再经 IPC 查询确认 `pause=true`。

## M3 计划（Lua 侧集成）

- dyn_menu.lua 增加 host-menu 消息：把播放列表/音轨等菜单数据序列化到临时 JSON，
  用 `Start-Process` 拉起 MpvMenuHost.exe（`--items` + `--pipe`），mpv 通过
  `--input-ipc-server` 提供管道；
- 宿主退出/点击后 mpv Lua 侧无需回写状态（命令直发 mpv 生效）。

## M3 已完成（2026-08-06）

- dyn_menu.lua 新增 `host_menu` / `host_menu_path` / `host_test_select` / `host_log`
  选项：播放列表/音轨按钮菜单优先走 WinUI 宿主（JSON → subprocess 拉起），
  宿主不可用或 `input-ipc-server` 未配置时自动回退 menu.dll 原生弹窗；
- dyn_menu.conf 已开启 `host_menu=yes`（指向本机构建产物，路径为机器特定，
  发布前需处理）；
- 端到端验证（`test\run_m3_e2e.ps1`）：真实 mpv 配置下触发 playlist-menu →
  宿主弹出 → 自动点击第 2 项 → mpv 执行 `playlist-play-index 1` →
  IPC 查询确认 `playlist-pos=1`；宿主干净退出。

## M4 待办（替换原生菜单路径）

- 发布前把 host 二进制的发布路径与 repo 打包方式定下来（自包含 WAS 目录较大）。

## M4 已完成（2026-08-06）

- 宿主支持子菜单级联：点击带 `submenu` 的条目 → 子菜单写临时 JSON → 新宿主进程
  对齐到父窗口右缘弹出（Win32 级联式）→ 叶子命令发 mpv 后退出；
- `hidden` 状态条目不再渲染（完整菜单的 播放/暂停 互斥隐藏正确处理）；
- 字符串 cmd 自动剥离 dyn_menu 的 `#menu:` 注解与状态表达式后再发 mpv；
- 右键完整菜单接入宿主：`show_button_menu` 优先走宿主（含全部子菜单），
  宿主不可用时回退原生；
- 运行中深浅色热切换：子类化窗口监听 WM_SETTINGCHANGE，切主题即刷新调色板；
- 两段式布局：窗口显示后重新测量内容并重设尺寸/位置（完整菜单 98x700 正常）。

### M4 验证（`test\run_m4_cascade_test.ps1`）

- 级联：父宿主 (1613,837) → 点击子菜单 → 子宿主对齐到 (1690,872) →
  自动选「暂停」→ mpv 执行 `cycle pause` → IPC 查询确认 `pause=true`；
- 右键：`script-binding dyn_menu/show` → 宿主进程拉起（完整菜单窗口出现）；
- 截图：`outputs\winui3-full-menu-prototype.png`（完整右键菜单）。

## 剩余（发布前）

- select 总菜单入口（右键菜单 > 工具 > 打开select总菜单）仍为 OSD 菜单，未迁移；
- 宿主打包进 mpv-lazy 的分发方式（自包含 WAS 目录 ~340 文件，或改 framework 依赖）；
- `host_menu_path` 已改为 `~~\tools\winui-menu-host\` 相对路径（repo/安装目录一致）；
- 多显示器 DPI（PerMonitorV2）仅本机 100% 缩放验证过。

## 视觉回归修复（2026-08-06 晚）

> 历史记录：以下为 v3.x 自绘架构的修复过程；v4.1 已整块替换为 MenuFlyout，
> 仅供追溯，不代表当前行为。

- 外边框：WinUI 1.6+ 对“不可调整大小的无边框窗口”会画一圈 DWM 边框（官方 issue
  #9978）；SetBorderAndTitleBar(false,false) 不清理 WS_CAPTION/WS_THICKFRAME，
  且 IsResizable=true 时 WinUI 会反复把 WS_THICKFRAME 加回、DWM 保持 6~7px 边框。
  现做法：IsResizable=false + 手动剥离边框样式位（SWP_FRAMECHANGED）+
  DWMWA_NCRENDERING_POLICY=DISABLED 禁止 DWM 非客户区渲染，实测
  GetWindowRect == DWMWA_EXTENDED_FRAME_BOUNDS（diff 0,0,0,0）。
- 毛玻璃：改用 DesktopAcrylicController（Thin 变体），TintColor/TintOpacity/
  LuminosityOpacity 可调；窗口从不激活会导致系统亚克力按“非活动”变暗变实，
  配置强制 IsInputActive=true 恢复明亮玻璃感。元素级 AcrylicBrush 已移除
  （之前 TintOpacity 0.55~0.72 + 不透明 fallback 把玻璃完全盖住）。
- 宽度：最小宽度 220→300px（内容 MinWidth=300，窗口随内容自适应）。
- 行高/字形：行 Padding 14,10（≈40px 高）、字号 12→13、图标 14、
  勾选改用 MDL2 E73E、级联指示符改用 MDL2 E76C、选中态用系统强调色。
- 白闪：构造阶段先按内容量近似尺寸并定位（Activate 前），Activate 后立即隐藏，
  Root.Opacity=0，pass2 布局完成后再显示并淡入；实测首帧即为 300x232 菜单尺寸，
  不再出现默认大尺寸首帧（动画开始前先把 Root.Opacity 置回 1，否则
  Composition 动画结束后会回落到本地值 0 导致菜单消失）。
- 图标字体：运行时检测 SegoeIcons.ttf——Win11 用 Segoe Fluent Icons（更现代），
  Win10 自动回落 Segoe MDL2 Assets；23 个码点经 fontTools 核验两个字体全部覆盖。
- 测试脚本编码：test\*.ps1 含中文注释时必须保持 UTF-8 带 BOM，否则 PowerShell 5.1
  按 ANSI(GBK) 解码会把中文注释末尾字节误读成双字节首字节、吞掉行尾换行，
  导致注释“吃掉”下一行代码（M3/M4 的窗口检测与右键检查偶发失败即此根因；
  已修复并改为 Process.MainWindowHandle 轮询，不再用 EnumWindows 委托）。
- 视觉回归门禁：`test\verify_host_window.ps1`（ASCII-only，无编码坑）拉起宿主后断言
  无边框（GetWindowRect == EXTENDED_FRAME_BOUNDS）、无 WS_CAPTION/WS_THICKFRAME、
  宽度 ≥300、UI Automation 内容树 ≥5 节点、亚克力初始化日志；示例菜单与 40 项
  长菜单（300x1032 高度钳制）均 PASS。
- 一键全量回归：`test\run_all.ps1`（verify 系统主题/深/浅/长菜单 + M2 mock +
  M2 真 mpv + M3 + M4 + 孤儿预热退出 + 宿主缺失回退），当前 10/10 PASS，约 45 秒。
- 真实右键 E2E：`test\run_rightclick_e2e.ps1`（真实 mpv + 部署配置 → 预热 →
  `script-binding dyn_menu/show` → 菜单在光标处弹出；会移动鼠标，勿在用户操作时跑；
  实测触发到可见 95–160ms、预热就绪 <1s）。注意：mpv-lazy 的 mpv.exe 包装器会重写
  命令行，启动参数必须整串传递、路径加引号、`--input-ipc-server` 用裸管道名。
- 深色路径验证：`--theme dark` 下 THEME dark=True、亚克力 tint=0.55、表面画刷
  0xAD alpha；浅色 tint=0.45、表面 0xD9（日志断言已并入 verify 脚本）。
- 临时文件卫生：宿主加载完菜单 JSON（Lua 的 temp-path 文件与级联子菜单
  mpv-menu-host-items-<pid>-<ticks>.json）后立即删除；全量回归跑完 %TEMP%
  零残留。历史泄漏可用 `test\cleanup_temp_items.ps1` 一次性清理。
- 回退路径实测：`test\run_fallback_test.ps1` 把 host_menu_path 指向不存在的文件，
  验证宿主不启动、mpv 打出“找不到 MpvMenuHost.exe…回退原生菜单”警告且不崩溃；
  repo 与安装目录 dyn_menu.conf 均为 host_menu=yes + `~~\tools\winui-menu-host\`
  相对路径，两侧一致。

## v3.5（2026-08-06 晚，点击/退出修复）

- 退出崩溃根治：WAS 2.3.1 未打包模式 `Application.Current.Exit()` 的 XAML 拆除路径
  会原生访问冲突（0xC0000005，M3/M4 的 test-select 关闭同样复现）。菜单进程无状态、
  钩子/WndProc 已还原，改为裸 `Environment.Exit(code)` 硬退出，实测退出码 0 无崩溃。
  `Close()` 增加 `_closed` 防重入。
- 看门狗：菜单是瞬态窗口，任何异常滞留都会让全局低层钩子吞掉后续点击/右键
  （表现为“右键第二次呼不出”“播放列表/音轨按钮无反应”）。后台线程 60 秒强制
  `Environment.Exit(0)` 兜底，与 UI 线程是否卡死无关。
- Lua 侧临时 JSON 改唯一文件名：`<temp>\mpv-menu-host-items-<pid>-<time>-<seq>.json`
  （temp-path 为空时回退 `%TEMP%`），快速连点不再互相覆盖；宿主加载后即删，
  级联子菜单文件沿用 `<pid>-<ticks>` 命名，全量回归后 %TEMP% 零残留。
- 钩子安装状态入日志（`HOOKS mouse=... kb=...`），便于排查吞输入问题。
- 验证脚本补 `SetProcessDpiAwarenessContext(PerMonitorV2)`：PowerShell 非 DPI 感知时
  GetWindowRect 返回虚拟化坐标，DWM EXTENDED_FRAME_BOUNDS 返回物理坐标，
  导致无边框断言假失败（86px 偏移）；补丁后 frame-diff=0,0,0,0。
- 实测：连续 4 次右键呼出全部成功（ESC 关闭后再次呼出正常）；真实点击子菜单行
  级联出子宿主；M3/M4 退出码 0；`run_all.ps1` 9/9 PASS。

## v3.6（2026-08-06 夜，体验与结构整理）

- 启动提速：Release 改为 ReadyToRun 发布（非裁剪，WAS 2.3.1 未打包下裁剪产物无法启动、
  publish 会漏 XBF/PRI，csproj 已补 Publish 后拷贝目标）；真实 mpv 触发到菜单可见
  平均约 336ms（Debug 约 360ms+，去掉动画后体感更快）。
- 去白闪：删除入场淡入/缩放动画，窗口显示即全不透明。
- 去圆角白线：菜单不再画 1px 描边（BorderThickness=0）。
- 间距收紧：行 Padding 14,10→12,7、标题 14,10,14,6→12,8,12,4、分隔线 12,8→12,6、
  外框 Padding 8,6→6,4（示例菜单窗口 525x403→525x329）。
- 二级菜单保持一级存活：点击子菜单后父窗口不再关闭，父子进程互相监听退出
  （`--parent-pid`），点击外部/ESC 时两侧先后关闭；父菜单钩子把子窗口区域视为内部。
- 子菜单靠右溢出自动翻转到父菜单左侧（按条目宽度估算）。
- 播放列表/音轨按钮菜单改为鼠标上方弹出：Lua 传 `--anchor bottom`（菜单底边对齐鼠标），
  右键菜单仍为鼠标处左上角弹出。
- input.conf 右键菜单整理：播放列表上移、章节/版本/轨道归位、滤镜相关合并为
  「滤镜与增强」（视频滤镜/着色器/增强脚本三个子组）、「关于」移到退出前、
  「重置 音频与字幕同步」并入音频区块、删除顶层冗余「播放」子菜单。

## v3.7（2026-08-06 深夜，预热与交互细节）

- 弹出提速：新增隐藏预热宿主（mpv 启动时 `--prewarm` 拉起，心跳文件每 1 秒
  刷新；Lua 触发优先走预热，宿主读触发文件后直接显示）。真实 mpv 触发到菜单可见
  约 90~200ms（此前冷启动 1 秒以上）；预热宿主在菜单关闭后回到隐藏待命，复用不退出。
- 触发链路：触发文件携带 JSON 路径/pipe/anchor/test-select；心跳 3 秒未更新视为
  死亡并回退普通拉起；新 mpv 会话启动时用 taskkill 清理上一会话残留预热进程。
- Lua 子进程全部改 `detach=true`：本 mpv 构建非 detach 的常驻子进程会阻塞脚本
  事件循环（曾导致预热后 dyn_menu 完全卡死）。
- 关闭菜单不再吞点击：外部点击穿透到 mpv（实测点击一次即切换暂停/播放）。
- 一级菜单置顶保持：显示期间每 100ms 重新 SetWindowPos(TOPMOST)，防止 mpv
  激活后盖住一级菜单。
- Windows 式展开方向：鼠标/目标点靠右时菜单向左展开、靠下时向上展开
  （右缘实测 3790→3265+525，底缘 2000→1594+406）。
- 播放列表/音轨菜单去掉 Lua 侧重复标题（宿主保留一个标题栏）。

## v3.8（2026-08-07 凌晨，级联细节）

- 子菜单与一级菜单留 4px 间隙，不再边缘重合。
- 置顶争夺修复：只有最末级菜单保持周期置顶，一级在打开子菜单后停止置顶循环，
  消除两级窗口互相抢顶层的闪烁。
- 切换二级菜单时自动关闭旧子菜单进程（同一父菜单下 A→B 不再残留 A）。

## v3.9（2026-08-07 凌晨，二级菜单提速）

- 二级菜单不再每次新起进程：父菜单显示时后台预拉起「下一级预热宿主」
  （id 形如 c<父pid>-<ticks>），点子菜单时写触发文件直接显示，
  触发到显示约 10~30ms（此前冷启动 300ms+）。
- 三级及以上级联同样逐级预热（每级显示时预拉起下一级）。
- 子级预热宿主在父会话结束时直接退出（不复位待命），父菜单重置/退出时
  主动杀掉子级预热宿主，无残留进程。
- 触发文件扩展第 5 行：子菜单携带精确位置（对齐父行右侧 + 4px 间隙）。

## v3.9.1（2026-08-07 凌晨，切换二级菜单崩溃修复）

- 切换二级菜单崩溃根因：每次重建都会新起一个「孙级预热宿主」监听任务，
  旧任务钉在旧 id/pid 上，读到陈旧心跳后误判孙级已退出，把整个菜单链关闭。
  改为全局唯一监听任务，每次循环读取当前 id/pid，重建/切换不再误关。
- 触发文件写入改独占+重试，失败回退普通拉起，父菜单不再因写锁竞态整体关闭。
- 重建前卸载旧钩子，避免切换时钩子重复安装泄漏。
- 会话代次守卫：旧触发的自动选择/IPC 结果不会在新菜单上生效或关闭它。

## v3.10（2026-08-07，三级菜单崩溃与二级闪烁修复）

- 三级菜单首次打开崩溃根因：EnumWindows 会跳过 DWM cloaked 窗口，子级预热
  在触发三级时短暂找不到父窗口句柄，误判“父会话结束”静默退出，整链崩溃。
  改为显式状态文件协议（显示写 open、复位写 closed，`--parent-prewarm-id`
  关联父实例），父子会话结束判定不再依赖窗口枚举。
- 二级多切换闪烁：有子级时本窗口停止周期置顶（只让最末级保持），
  鼠标移回本窗口时由 WM_MOUSEMOVE 抬升，双窗口不再争夺顶层渲染。

## v3.10.3（2026-08-07，链序/方向/白框修复）

- 链式置顶改用子级心跳文件里的窗口句柄：EnumWindows 会跳过 cloaked 窗口，
  父级原先找不到子窗口就插到最顶，导致链序反转、被视频窗口盖住。
  实测三级链序稳定为「子 > 父 > 视频」，mpv 置顶模式下也保持。
- 子级展开方向继承上级（`--side left|right`）：父菜单在左侧时子菜单继续往左，
  不再错误地往右弹回父菜单方向；所选方向放不下时才翻转到另一侧。
- 显示前重设 DWM 无边框/圆角属性 + 首帧透明（渲染完成后再恢复不透明度），
  消除二级菜单出现时的白色描边框闪现。

## v3.10.6（2026-08-07，实机反馈三项修复）

- 悬停转圈圈：根因是每次菜单关闭后子级预热进程都被杀掉，下一次悬停/展开/切换
  子菜单时冷启动新进程（系统显示 app-starting 转圈）。改为 mpv 启动时预拉全链
  （深度 0/1/2 三层常驻，`--depth` 控制），关闭菜单后不再杀子级、不再拦截
  （状态文件 `2`=待命，RESET 触发回复位），展开任意 3 级菜单全程零冷启动。
- 三级方向：级联子菜单不再翻转到父菜单另一侧；右侧链一直向右（放不下只右对齐
  钳制），左侧链一直向左；仅根菜单保留 Windows 式「靠右向左」。
- 置顶：父级只在子窗口可见时才插入其下方（隐藏的预热窗口不再把父级带出置顶带，
  避免被视频窗口盖住）；置顶循环提速到 100ms。
