# mpvw 项目总览与对齐报告

**日期**：2026-10-21
**基线**：仓库 `C:\Users\z2637\Documents\DeepSeek\mpv`，分支 `main`，HEAD `7b42fd5`
**测量方式**：全部由命令与源码检索得出（证据附在每节末尾），未沿用任何旧结论。

---

## 0. 一句话结论

mpvw 是一个**三层结构**的 WinUI 3 播放器：C# 界面层 + C++/WinRT 原生层 + mpv 配置/Lua 层。
当前 HEAD **构建 0 错误、17 个自检器全绿、测试 496 + 26 全通过、部署与构建产物一致**。
项目不缺功能，缺的是**结构可读性**：`Modules/` 下 13 个目录里有 5 个是「按实现机制」而不是
「按功能域」命名的（`Common`、`Mpv`、`Motion`、`MpvConf`、`Menu`），且 3 个文件超过 100 KB
（最大 258 KB），这正是「上下文过大、每次只能局部打转」的直接原因。

---

## 1. 项目结构总览

### 1.1 三个工程（解决方案 `mpv-winui/mpv-winui.slnx`）

| 工程 | 类型 | 职责 | 位置 |
|---|---|---|---|
| `mpv-winui` | C# WinUI 3 应用 | UI、页面、设置、播放控制、本地化 | `mpv-winui/mpv-winui/` |
| `mpv-winrt` | C++/WinRT 组件 | libmpv 宿主、缩略图预览器、事件与属性桥 | `mpv-winui/mpv-winrt/` |
| `mpv-winrt-projection` | C# 投影 | 为测试提供可引用的 WinRT 投影 | `mpv-winui/mpv-winrt-projection/` |
| `mpv-conf-test` | net10.0 测试 | mpv.conf 解析/写回、纯逻辑（布局、调色、频谱、拼音索引…） | `mpv-winui/mpv-conf-test/` |
| `mpv-winrt-test` | net10.0 测试 | 真机驱动 `mpv_winrt`（封面、标签、属性） | `mpv-winui/mpv-winrt-test/` |

`mpv-winui-lazy/` 不是工程，是被 `csproj` 以 `Content Include` 整目录拷进输出、首次运行由
`ConfigDeployer` 部署到 `%LOCALAPPDATA%\mpv-winui\mpv` 的**配置层**。

### 1.2 C# 应用内部（`mpv-winui/mpv-winui/`）

| 目录 | 文件数 | 用途 | 性质 |
|---|---|---|---|
| `App.xaml(.cs)` `Program.cs` | 3 | 入口；`App.xaml` 合并字典 + 品牌 accent 色阶 | 根，不可移动 |
| `Shell/` | 7 | `MainWindow`（导航壳、标题栏、尺寸、宽高比、PiP 宿主）+ `AppContext`（全局服务） | 外壳 |
| `Modules/Player/` | 79 | 播放页、控制栏三套皮肤、PiP、队列、库、音乐、缩略图、菜单栏、快捷面板 | 功能域（过大） |
| `Modules/Settings/` | 87 | 设置窗口/页面、600+ 选项行、布局定制、mpv.conf 读写 | 功能域（过大） |
| `Modules/Common/` | 33 | 转换器、控件扩展、工具、窗口管理、图标表 | **机制型，应下沉** |
| `Modules/MpvConf/` | 22 | mpv.conf 编辑器/查看器、schema、选项渲染 | 功能域 |
| `Modules/Menu/` | 14 | 菜单编辑器、`menu.conf` 解析写入、菜单栏服务 | 功能域 |
| `Modules/Language/` | 9 | `AppLang`（2208 属性 × 9 语言）、语言文件、定制文案 | 服务 |
| `Modules/MediaInfo/` | 6 | MediaInfo 窗口与 `MediaInfo.dll` 封装 | 功能域 |
| `Modules/Motion/` | 5 | 缓动、弹簧数学、行为附加属性、动效开关 | **机制型（基础设施）** |
| `Modules/FileSystem/` | 5 | 文件服务、选择器、配置部署、AppData 路径 | 服务 |
| `Modules/Activation/` | 3 | 文件关联 / `mpvw://` 协议激活 | 服务 |
| `Modules/Mpv/` | 3 | Win32 键盘钩子、键码表 | **平台层** |
| `Modules/AppModel/` | 1 | 打包/非打包判定 | 服务 |
| `Modules/About/` | 2 | 关于页 | 功能域 |
| `Assets/` `Languages/` `Menus/` `MpvConfOptions/` `Styles/` `Themes/` | 53/9/1/4/1/1 | 图标、9 种语言 JSON、`menus.json`、mpv 选项 schema、全局样式、隐式样式 | 资源 |

计数见 §8 证据 E1/E2。

### 1.3 C++/WinRT 组件（`mpv-winui/mpv-winrt/`）

`MpvPlayer.{h,cpp,idl}`（libmpv 生命周期、命令、属性观察、事件）、`MpvPreviewer.{h,cpp,idl}`
（第二个 libmpv 实例，软件渲染，供进度条缩略图）、`Types/`（12 个值类型）、`Events/`（15 组
事件参数）。**MIDL 的 `import` 相对工程目录解析**，不是相对文件——移动 IDL 时要改 import 路径。

### 1.4 配置层（`mpv-winui-lazy/`）

`mpv.conf`（20 KB，主配置）、`input.conf`（14 KB，键位 + `#menu:` 注解，右键菜单的真正来源）、
`profiles.conf`、`script-opts.conf`、`scripts/`（18 个 Lua）、`script-opts/`、`fonts/`、`licenses/`、
`docs/`、`tools/`、`vs/`。

### 1.5 仓库其他目录

| 目录 | 内容 | 状态 |
|---|---|---|
| `tools/` | 26 个 `.py` 检查器/生成器 + 5 个 `.ps1`/`.sh` | 活跃，是构建门禁的一部分 |
| `docs/` | 18 份文档（设计契约 74 KB、设置评审 41 KB、审查任务书 32 KB…） | 活跃但有过期项 |
| `installer/` | WiX `product.wxs` + `build-installer.ps1` | 活跃 |
| `appdata-sample/` | 手工样本（`%LOCALAPPDATA%\mpv-winui` 的参考副本） | 参考 |
| `screenshot/` | 6 张 README 用截图 | 文档资源 |
| `.github/workflows/build.yml` | CI | 活跃 |
| `dist/` | 3 个旧发布产物（MSI 77 MB + ZIP 91 MB） | **被 .gitignore 忽略，不入库** |
| `_work/` `_cloud/` | 本地临时与云端克隆 | 未跟踪 |

---

## 2. 功能地图

### 2.1 导航与页面关系

主窗是一个 `NavigationView` + 单个 `Frame`（`ShellFrame`），**不是**每页一个 `Page`：

```
MainWindow (NavigationView + Frame)
  └─ MpvPlayerPage.xaml              唯一"页面"，用 Visibility 切换五个宿主
       ├─ LibraryHost       ← home / videos
       ├─ QueueHost         ← queue
       ├─ MusicHost         ← music
       ├─ PlayerView        ← 播放中（视频或音频）
       └─ 播放页覆盖层       ← 控制栏、菜单栏、信息条
```

导航项在 `MainWindow.xaml.cs:175` 的 `ShellSections` 表里，**四项**：`home` / `music` / `videos` /
`queue`。`playlists` 已被删除（注释写明理由：与 queue 重复）。选中变化 → `ShellNav_SelectionChanged`
→ 调 `MpvPlayerPage` 的 section 切换方法（`_librarySection` 字段，常量定义在
`MpvPlayerPage_Library.cs:56-67`）。

### 2.2 功能域表

| 功能 | 入口 | 职责 | 状态来源 | 主文件 |
|---|---|---|---|---|
| **主页** | 导航 `home` | 最近/继续观看的空态与列表 | `WatchHistory` / `AppSettings` | `MpvPlayerPage_Library.cs`、`History/WatchHistoryControl` |
| **音乐库** | 导航 `music` | 歌曲/专辑/艺人三个 Tab、拼音 A-Z 索引、专辑页 | `AppSettings.MusicLibraryFolders` + 扫描 | `MpvPlayerPage_Music.cs`（24 KB）、`LibraryEntry.cs`、`LibraryIndexLetter.cs` |
| **视频库** | 导航 `videos` | 全部/文件夹两个 Tab、16:9 卡片、文件夹头 | `AppSettings.VideoLibraryFolders` | `MpvPlayerPage_Library.cs`（126 KB） |
| **播放队列** | 导航 `queue` | mpv 自带 playlist 的视图 + 侧面板变体 | mpv `playlist` 属性 | `MpvPlayerPage_Playlist.cs`、`_PlaylistPanel.cs` |
| **播放页** | 打开文件后自动 | 视频表面、控制栏、全屏、PiP、宽高比 | mpv 属性事件 | `MpvPlayerPage.xaml.cs` + 12 个 partial、`PlayerControl.*` |
| **设置页** | 标题栏齿轮 → `SettingsWindow` | 6 大类 69 个 section、~700 行选项、布局定制 | `AppSettings` + `mpv.conf` | `SettingsPage.xaml(.cs)`（49/70 KB）+ 14 个 partial |
| **mpv.conf 编辑器** | 设置 → Program → 配置 | 文本编辑、profile 片段、schema 补全 | 部署的 `mpv.conf` | `MpvConf/` |
| **菜单编辑器** | 设置 → Program → 关联/菜单 | `menus.json` 树 → `menu.conf` | `Menus/menus.json` | `Menu/MenuEditor/`（14 文件） |
| **右键菜单** | 播放页右键 | dyn_menu 的 `menu-data` → `Flyout` | `input.conf` 的 `#menu:` 注解 | `MpvPlayerPage_MpvMenu.cs`、`Menu/MpvMenu/` |
| **媒体信息** | 菜单栏 / 快捷键 | `MediaInfo.dll` 报告窗口 | 当前文件 | `MediaInfo/` |
| **截屏** | 菜单栏 / 快捷键 | 格式、目录、模板、JXL/AVIF/WEBP 质量 | `AppSettings.Screenshot*`（16 项） | `AppSettings.Screenshot.cs`、`SettingsPage.Options.Screenshot.cs` |
| **拖放** | 窗口任意处 | 文件/文件夹/URL 落点判定 | 拖放事件 | `MpvPlayerPage_Drop.cs` |
| **主题/皮肤** | 设置 → Interface → Player appearance | `ThemeType` / `BackdropType` / `PlayerStyle` / `UiFont` | `AppSettings` + 3 套 XAML Style | `App.xaml`、`Styles.xaml`、`Compact/CenterPlayerControlStyle.xaml` |
| **本地化** | 设置 → Program → 语言 | 2208 属性 × 9 语言 | `Languages/*.json` | `Language/` |

---

## 3. 术语表（本文档为全项目唯一口径）

| 术语 | 定义 | 不是 |
|---|---|---|
| **主页 (Home)** | 导航第一项，展示历史/继续观看 | 不是"启动页"，也不承担库功能 |
| **音乐库 (Music library)** | 绑定音乐文件夹后的歌曲/专辑/艺人视图 | 不是"播放列表" |
| **视频库 (Video library)** | 绑定视频文件夹后的卡片网格 | 不是"最近播放" |
| **播放页 (Player page)** | 打开媒体后覆盖整个内容区的播放视图（含控制栏、菜单栏、视频表面） | 不是独立 `Page`，是 `MpvPlayerPage` 的一个宿主 |
| **状态栏** | 播放页底部那条 64 dip 高的传输条（播放/进度/音量/时间） | 不是 Windows 状态栏，不含 `StatusBar` 控件 |
| **导航栏 (Navigation bar)** | 左侧 `NavigationView` 面板（自定义宽 240 / rail 48 / 覆盖式） | 不是标题栏 |
| **播放队列 (Play queue)** | mpv 自己的 `playlist`；导航 `queue`；侧面板是它的第二个视图 | 不是历史记录 |
| **播放列表 (Playlist)** | **本项目中已废弃的导航项**，曾与队列重复，已删除 | 不要与新代码中的 "playlist" 变量混淆（那指 mpv playlist） |
| **媒体信息 (Media info)** | `MediaInfo.dll` 解析出的技术报告窗口 | 不是"标签/元数据"（后者是 `MusicTags`） |
| **截屏 (Screenshot)** | mpv 的 `screenshot` 命令 + 应用的格式/目录设置 | 不是"截图验证"（后者指 UI 自动化取证） |
| **绑定 (Bind)** | 把一个文件夹加入音乐库或视频库（`AddLibraryFolder`），可解绑 | 不是 XAML 数据绑定 |
| **皮肤 (Skin)** | **歧义术语，需分层**：L1 应用级主题（`ThemeType`）；L2 播放器控制栏皮肤（`PlayerStyle`：Standard/Centre/Compact）；L3 mpv 进程内 OSC（本项目未使用） | 不是单一概念；详见 Skin 报告 |
| **section** | 设置页的二级卡片（69 个） | 不是代码里的"分区" |
| **category** | 设置页的一级分类（6 个：Program/Video/Audio/Subtitles/Playback/Interface） | — |

---

## 4. 规范基线

### 4.1 符合 WinUI 3 官方规范的部分

| 项 | 证据 |
|---|---|
| `NavigationView` + `Frame` 单页壳、`PaneDisplayMode=Auto` 三档 | `MainWindow.xaml` |
| `ExtendsContentIntoTitleBar` + `Window.SetTitleBar(控件)`（不是 `AppWindow.TitleBar` 那套） | `MainWindow.xaml.cs` |
| `ResourceDictionary` + `MergedDictionaries` + 主题字典 | `App.xaml`、`Styles.xaml` |
| 主题色走 `SystemAccentColor*` 七色阶覆盖（画刷级覆盖实测无效） | `App.xaml` 注释与实测记录 |
| Fluent 图标 `Segoe Fluent Icons`，码位逐个渲染核对 | `MainWindow.xaml.cs:163` 注释 |
| 主题资源 `{ThemeResource}` / `{StaticResource}` 分工 | `Styles.xaml`（119 个 `x:Key`） |
| `OptionType`(11) → `OptionTemplateSelector` → `DataTemplate` 1:1:1 | `Modules/Settings/Controls/` |

### 4.2 历史遗留 / 偏离官方规范

| 项 | 位置 | 说明 |
|---|---|---|
| `Modules/Common/` 混装 UI 控件、转换器、工具、窗口管理 | 33 文件 | 不是功能域，违反"按域分区" |
| 三个超 100 KB 的 partial 文件 | `SettingsPage.Options.Gen.cs`(259 KB)、`AppLang.cs`(164 KB)、`AppLang.Gen.cs`(130 KB)、`MpvPlayerPage.xaml`(131 KB)、`MpvPlayerPage_Library.cs`(126 KB) | 生成文件本身合理，但 `AppLang.cs` 与 `MpvPlayerPage_Library.cs` 是手写的 |
| `SettingsPage` 用 14 个 partial 表达一棵树 | 70 KB 主文件 + 13 个 partial | 树结构分散在 `Options.*.cs` 里，`sectionMap` 是唯一入口（设计上故意 fail-loud） |
| 手写代码与生成代码同名不同源 | `AppLang.cs` / `AppLang.Gen.cs`；`MpvSettings.cs` / `MpvSettings.Gen.cs`；`SettingsPage.Options.cs` / `.Gen.cs` | 检查器必须同时读两半，历史上 6 个检查器只读一半 |
| 页面用 `Visibility` 切换而非 `Frame.Navigate` | `MpvPlayerPage` | 与 WinUI 导航惯例不同，但有明确理由（视频表面不能重挂） |

### 4.3 状态管理基线

- **`AppContext`（静态）** 是唯一共享状态出口：`AppLang`、`AppSetting`、`SettingChanged` 事件、
  `RunMpvCommand`/`SetMpvLogLevel` 由播放页挂接、`GetAudioDevices`/`GetGpuAdapters` 枚举委托。
- **`AppSettings`** 持久化到注册表 `HKCU\...\mpv-winui\mpv-winui\app`，布尔存 `"true"/"false"`。
- **`mpv.conf` 是设置的真相源**：启动时 `MpvSettings.LoadFromConfig()` 先读文件再写回；
  `ManagedMpvConfig` 写应用管理的块，`MpvConfOverrides` 判断"文件里已有的选项是否压过设置"，
  `MpvConfWatcher` 在窗口打开时重读。
- **命令顺序**：`MpvCommandQueue` FIFO；输入转发直连（不排队）。

### 4.4 动效基线

`Modules/Motion/`：`MotionTokens`（时长/曲线令牌）、`MotionEasing`、`SpringMath`（纯函数，被
`mpv-conf-test` 链接）、`MotionGate`（系统"减少动画"开关）、`MotionBehaviors`（附加属性：
FadeIn/Reset 等）。**没有过冲**——`21ce531` 把 `SpringPop` 改成 `Move` 曲线。

### 4.5 构建与部署链路

```
build.ps1 -Configuration Release -Platform x64
  ├─ python tools/check-xaml.py             （XAML 注释不能含 "--"）
  ├─ python tools/check-script-opts.py
  ├─ python tools/check-winrt-events.py
  ├─ MSBuild mpv-winrt.vcxproj              → mpv-winui\bin\x64\Release\mpv_winrt\
  ├─ dotnet build mpv-winui.csproj          → mpv-winui\mpv-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\
  ├─ dotnet test mpv-conf-test              （496 通过）
  └─ dotnet test mpv-winrt-test             （26 通过）
tools/deploy-test.ps1 -CloseRunning -Verify  ← 把上面 win-x64 目录叠拷到 mpv-deploy\
compare-deploy.ps1                           ← 逐文件比对 + .xbf 计数 + 剪掉孤儿 .xbf
```

关键事实：
- **程序集名是 `mpvw`**（`<AssemblyName>mpvw</AssemblyName>` + `<TargetName>` 同值），不是 `mpv-winui`。
- **自包含**：`SelfContained=true` + `WindowsAppSDKSelfContained=true`，根目录几百个 dll **不能**收进子目录。
- `Modules/ Shell/ Styles/ Themes/` 目录**只存在于 build 输出**；`publish` 把 `.xbf` 内联进 `resources.pri`。
- 部署是**叠拷 + 剪枝**，绝不删目录（WinAppSDK 运行时 dll 只存在于目标目录）。

---

## 5. 本轮已完成的验证（实测，非声称）

| 项 | 命令 | 结果 |
|---|---|---|
| 构建 | `pwsh ./build.ps1 -Configuration Release -Platform x64` | exit 0，0 错误，6 个警告（AOT/trim 提示 + 1 个 NU1603） |
| mpv-conf 测试 | 同上链内 | 496 通过 / 0 失败 |
| mpv-winrt 测试 | 同上链内 | 26 通过 / 0 失败（2 个按条件跳过） |
| XAML | `python tools/check-xaml.py` | 40 个 XAML 全部良构 |
| 本地化 | `check-localization.py` | 2208 属性 × 9 文件一致 |
| 设置树 | `check-sections.py` | 69 sections / 6 categories / 68 唯一图标 |
| 设置漂移 | `check-settings-drift.py` | 701 AppSettings / 633 mpv 映射 / 686 选项键 |
| 其余 13 个检查器 | 见 §8 E3 | 全部 exit 0 |
| 部署 | `pwsh ./tools/deploy-test.ps1 -CloseRunning -Verify` | 499 文件 identical，0 differing，xbf 40=40 |

---

## 6. 待确认问题（需要你拍板，会影响后续分区与清理）

1. **分区方案要不要动文件名？** 我倾向"新增 `Modules/<Domain>/README.md` 上下文入口 + 把
   `Common/Motion/Mpv` 三个机制型目录明确标注为基础设施层"，而**不**大规模移动文件——移动会
   触发 XAML 资源 URI、`csproj` 显式 `Content Include`、以及 `docs/settings-design-contract.md`
   里 33 条已知例外。如果你要的是"物理重排目录"，代价与收益我需要先给出评估。
2. **`_modules/Player` 79 文件、`Settings` 87 文件是否要拆 subdir？** 拆 `Player/Library/`、
   `Player/Transport/`、`Player/PiP/` 是可行的，但 `partial class MpvPlayerPage` 跨目录时
   要保证同一个 `namespace`，且 `csproj` 用的是默认通配（无显式列出），所以技术上无阻。
3. **`docs/` 里 18 份文档有 4 份已被后续提交推翻**（见干扰项清单）。是删除、还是移到
   `docs/archive/`？我建议后者。
4. **`mpv-winui-lazy/scripts/` 里 `dyn_menu.lua`(66 KB) 与 `dynamic_menu.lua`(6.6 KB) 并存**：
   前者是 mpv-menu-plugin 的 GPL 源 + 本项目补丁，后者是本项目写的 VSR/HDR 动态子菜单，
   通过 `menu-ready` 脚本消息串联。**它们不是重复**。确认后我会在 `VERSIONS.md` 里写清关系，
   避免下一轮再被误判为重复项。
5. **Skin/SCP 的定义**：见 §3 术语表，我主张 L1+L2 才是本项目语境下的"Skin"，L3（mpv OSC）
   明确不引入（进程内嵌入，没有独立 mpv 窗口可以画 OSC）。
6. **`screenshot/` 目录 6 张图是 README 引用还是历史产物？** `README.md` 未引用它们。

---

## 7. 下一阶段（按你给的交付顺序）

1. ✅ 本报告（总览与对齐）
2. → 功能分区方案（含每区 `README.md` 模板）
3. → 云端差异矩阵（已克隆 `_cloud/mpv-winui-player`，结论：本地是云端的**严格后继**，
   领先 116 提交、落后 0；细节见差异审计报告）
4. → 干扰项清单与清理方案
5. → 执行清理 + 重组 + 重新部署
6. → Git 接入（**已完成**：`cloud` remote 指向 `saillill/mpv-winui-player`，`cloud/main`=3053352）
7. → MCP 集成与端到端验证
8. → Skin/SCP 报告
9. → 最终运行验证与改动清单

---

## 8. 证据索引

**E1** 文件计数（每目录）
```powershell
Get-ChildItem Modules -Directory | ForEach-Object {
  $files=(Get-ChildItem $_.FullName -Recurse -File | Measure-Object).Count
  $kb=[math]::Round((Get-ChildItem $_.FullName -Recurse -File | Measure-Object Length -Sum).Sum/1KB)
  "{0,-24} {1,4} files {2,7} KB" -f $_.Name,$files,$kb }
```

**E2** 源文件规模
```powershell
Get-ChildItem -Recurse -File -Include *.cs,*.xaml |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|packages|AppPackages)\\' } |
  Sort-Object Length -Descending | Select-Object -First 12 Name,Length
```
→ 329 个 `.cs`、80 个 `.xaml`；最大 5 个：`SettingsPage.Options.Gen.cs` 258828、
`AppLang.cs` 164432、`MpvPlayerPage.xaml` 131359、`AppLang.Gen.cs` 129508、
`MpvPlayerPage_Library.cs` 125617。

**E3** 17 个检查器
```powershell
foreach ($c in @('check-xaml','check-localization','check-sections','check-settings-drift',
  'check-empty-sections','check-conf-lines','check-script-opts','check-winrt-events',
  'check-ui-tooltips','check-visual-state-layout','check-section-icons','check-search-index',
  'check-placeholders','check-resources','check-scroll-maxwidth','check-mpvconf-precedence',
  'check-conflict-rules')) { & python "tools/$c.py"; "exit=$LASTEXITCODE" }
```

**E4** 构建
```powershell
pwsh -NoProfile -File .\build.ps1 -Configuration Release -Platform x64
# 6 个警告 / 0 个错误；mpv-conf-test 496 通过；mpv-winrt-test 26 通过
```

**E5** 部署一致性
```powershell
pwsh -NoProfile -File tools/deploy-test.ps1 -CloseRunning -Verify
# copied 499, identical 499, differing 0, xbf 40 (built 40)
# only in dst: mscordaccore_...826.23019.dll, native\mpvw.exe（叠拷残留，非缺失）
```
