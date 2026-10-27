# 项目现状基线（从代码重新梳理）

> **证据等级**：【已运行】本轮实际运行确认 ｜【代码】源码/git 静态事实 ｜【推断】未运行 ｜【未知】需补充
>
> 基线：`main` @ `0d0e889`，2026-10-26。不引用仓库内既有文档的任何结论。

---

## 1. 仓库结构与职责

```
mpv-winui/                      解决方案根（mpv-winui.slnx）
  mpv-winui/                    C# WinUI 3 应用（主体，本项目的全部 UI）
    App.xaml(.cs) Program.cs    入口，必须在项目根
    Shell/                      MainWindow + 窗口分部类 + AppContext
    Modules/<Area>/             按功能域分目录（见下）
    Assets/ Languages/ Menus/ Styles/ Themes/ MpvConfOptions/   随输出复制
    NativeMethods.* app.manifest Package.appxmanifest            MSBuild 约定，必须在根
  mpv-winrt/                    C++/WinRT 组件（libmpv 运行时封装）
  mpv-winrt-projection/         C# 投影（供测试引用，产出 nupkg）
  mpv-conf-test/                xUnit 测试（527 用例，纯逻辑）
  mpv-winrt-test/               xUnit 测试（26 用例，投影层）
  libs/                         mpv 开发库（gitignore，需 fetch 或手工放置）
  packages/                     NuGet packages.config 还原目标（gitignore）
mpv-winui-lazy/                 配置层（mpv 主配置必须在根，故独立目录）
tools/                          58 个 Python 自检 + 5 个 PowerShell 构建/部署脚本
tools/mvpw-mcp/                 自带 MCP 服务器 + 60 个验证器（未注册到 dsh）
tools/ui-repair/                长周期 UI 修复循环的驱动脚本
docs/                           既有文档 30+ 份（未采信，见 ISSUE_LOG 的"不采信"原则）
appdata-sample/                 %LOCALAPPDATA%\mpv-winui 的手工样例
installer/  dist/  screenshot/  打包与截图资产
_cloud/                         gitignore：云端仓库的本地快照副本
_work/                          gitignore：一次性探针与截图（约 545 文件）
```

**解决方案内 5 个项目**【代码】`mpv-winui.slnx`：`mpv-conf-test`、`mpv-winrt-projection`、`mpv-winrt-test`、`mpv-winrt`(vcxproj)、`mpv-winui`(csproj)。

---

## 2. 页面清单

应用只有 **1 个主窗口 + 1 个播放页 + 3 个独立 Window + 1 个 PiP 窗口**，其余都是 UserControl。导航不是 `Frame` 多页路由，而是**同一个 `MpvPlayerPage` 内部切换 library section**。

| 界面 | 类型 | 入口 | 职责 | 关键控件 | 状态来源 |
|---|---|---|---|---|---|
| `Shell/MainWindow.xaml` | Window | `Program.cs` → `App.xaml.cs` | 外壳：标题栏、导航、搜索、播放条、内容 Frame | `ShellNav`(NavigationView)、`ShellTitleBar`、`ShellLibrarySearch`、`ShellPlayerBar`、`ShellSettingsButton`、`ShellOpenFileButton` | 【代码】`Shell/MainWindow.xaml:160-493`；`MainWindow.xaml.cs` |
| 主页 home | `MpvPlayerPage` 的 section | 导航 `ShellNav_home` | 全部媒体的平铺列表 + 最近 | `LibrarySectionTitle`、`LibraryGrid`、`LibraryOpenFileButton` | 【代码】`MpvPlayerPage_Library.cs:57` |
| 音乐库 music | 同上 | `ShellNav_music` | 专辑/艺术家/歌曲三种视图 + 字母栏 + 排序 | `MusicTabAlbums`、`MusicTabArtists`、`AlbumCardCoverImage`、`SongCardCoverImage`、`LibraryIndexRail`、`SortByNameAsc`… | 【代码】`MpvPlayerPage_Library.cs:59`、`MpvPlayerPage_Music.cs` |
| 视频库 videos | 同上 | `ShellNav_videos` | 文件夹视图 / 平铺视图 | `VideoTabFolders`、`VideoAddFolderButton`、`VideoFolderGroups`、`VideoSortButton`、`FolderCoverImage` | 【代码】`MpvPlayerPage_Library.cs:61` |
| 播放队列 queue | 同上 | `ShellNav_queue` | mpv playlist 的行视图 | `QueueEmptyTitle`、`QueueClearButton` | 【代码】`MpvPlayerPage_Library.cs:68` |
| 播放列表 playlists | **无入口** | 无 | 常量仍在，switch 分支仍在 | 无 | 【代码】`MpvPlayerPage_Library.cs:70` + 5 处分支；导航项 `MainWindow.xaml.cs:187-190` 只有 4 项 |
| `MpvPlayerPage.xaml` | Page（2418 行 XAML） | `ShellFrame` 唯一内容 | 播放器 + 媒体库二合一 | 143 个 `x:Name` | 【代码】 |
| `SettingsWindow.xaml` + `SettingsPage.xaml` | Window + Page | `ShellSettingsButton` | 设置（树形，含 356 条生成行） | `OptionListControl` | 【代码】`Modules/Settings/`，21 个 `SettingsPage*.cs` 分部共约 1.4 万行 |
| `PiPWindow.xaml` | Window | `PiPButton` | 画中画 / 迷你播放器，复用 `PlayerControl` | — | 【代码】`MpvPlayerPage_PiP.cs` |
| `MediaInfoWindow.xaml` | Window | 右键菜单 | MediaInfo 详情 | `MediaInfoUserControl` | 【代码】`Modules/MediaInfo/` |
| `MenuEditorWindow/Page` | Window + Page | 设置内入口 | 右键菜单编辑器 | — | 【代码】`Modules/Menu/MenuEditor/` |
| `MpvConfEditorWindow/Page`、`MpvConfTextWindow` | Window + Page | 设置内入口 | mpv.conf 编辑器 | `MpvConfOptionControl` | 【代码】`Modules/MpvConf/` |

### 运行时可读的导航项【已运行】
`winapp ui inspect -a mpvw` 读到 4 个：`ShellNav_home`("主页")、`ShellNav_music`("音乐库")、`ShellNav_videos`("视频库")、`ShellNav_queue`("播放队列")。

---

## 3. 服务与通信层

| 服务 | 实现位置 | 说明 |
|---|---|---|
| mpv 运行时 | `mpv-winui/mpv-winrt/`（C++/WinRT） | `MpvPlayer.*`、`MpvPreviewer.*`；事件 29 个声明、21 个有 native raise【已运行】`check-winrt-events` |
| 播放页状态 | `Modules/Player/MpvPlayerPage_*.cs`（21 个分部，约 7000 行） | 播放控制 / 库 / 菜单 / 拖放 / 输入转发 / 显示监控 / PiP / 全屏 各自独立分部 |
| IPC | `\\.\pipe\mpvpipe` | 【已运行】自带 `read_playback_state` 工具走此管道；连接建立后约 0.4s 断开，需一次握手续完所有请求 |
| 内置预览（缩略图） | `MpvPreviewer.cpp` + `MpvPlayerPage_Preview.cs`(262 行) + `BuiltInPreview/MpvSoftwarePreviewControl` | 进程内第二个 libmpv 实例，软件渲染 |
| 音频频谱 / 封面配色 | `Modules/Player/Audio/`：`AudioCapture.cs`(235)、`SpectrumAnalyzer.cs`(259)、`AlbumColorExtractor.cs`、`ArtworkPalette.cs` | 有对应单测（`SpectrumAnalyzerTests`、`SpectrumDecayTests`、`AlbumColorExtractorTests`）在 527 用例内 |
| 媒体库 | `MpvPlayerPage_Library.cs`(2962 行) + `LibraryEntry.cs` + `LibraryEntry.Selection.cs` + `LibraryTagsReader.cs` + `MusicTags.cs` + `PinyinInitial.cs` | 扫描、分组、字母索引、选择模式 |
| 封面加载 | `MpvPlayerPage_Library.cs` 中 57 处 thumbnail 处理 + 原生层封面导出 | 【已运行】`verify_music_cards` 断言专辑/艺术家封面 stddev 13.7 / 88 色 |
| 设置持久化 | `Modules/Settings/`：`AppSettings*.cs`(默认值) + `MpvSettings.cs`(→ 命令行) + `SettingValueCodec.cs` + `CachedDataSetting.cs` | 存注册表 `HKCU\...\mpv-winui\mpv-winui\app`【代码：`Modules/Settings/` 内 registry 调用】 |
| 配置部署 | `Modules/FileSystem/ConfigDeployer.cs` | 每次启动把 `mpv-winui-lazy` 复制到 `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv`，`UserOwnedFiles` 除外 |
| 日志 | `Modules/Common/Utils/LoggerHelper.cs` | `%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\`（注意双层目录） |
| 动效 | `Modules/Motion/`：`MotionBehaviors`、`MotionEasing`、`MotionGate`、`MotionTokens`、`SpringMath` | 纯函数 + 单测 |
| 多语言 | `Modules/Language/` + `Languages/*.json`（9 种语言） | `check-localization.py` 校验 |
| 激活 / 协议 | `Modules/Activation/`：`ActivationService`、`ActivationRegistrationService`、`MpvProtocolUri` | 文件关联、`mpv://` 协议、启动参数 |

---

## 4. 资源与样式

| 资源 | 位置 | 规模 |
|---|---|---|
| 主样式字典 | `Styles/Styles.xaml` | 1521 行 / 120 个 `x:Key`【代码】 |
| 播放条布局皮肤 | `Modules/Player/CenterPlayerControlStyle.xaml`(758)、`CompactPlayerControlStyle.xaml`(544) | 由 `App.xaml` 合并 |
| 主题字典 | `Themes/Generic.xaml` | 12 行，0 个 key（近乎空壳）【代码】 |
| 导航面板主题色 | `Shell/MainWindow.xaml:196-269` 内联 `ResourceDictionary.ThemeDictionaries` | 亮/暗/高对比各一套 |
| 图标字体 | `Assets/FluentSystemIcons-Regular.ttf` + `Modules/Common/View/FluentIcons*.cs` + `IconFonts.cs` | 字体图标，非位图 |
| 应用图标 | `Assets/*.png`（53 个文件）、`App.ico` | — |
| 菜单数据 | `Menus/`（1 个文件）、`MpvConfOptions/`（4 个 json） | — |

**合并入口**【代码】`App.xaml`：`Styles/Styles.xaml` + `CenterPlayerControlStyle.xaml` + `CompactPlayerControlStyle.xaml`。

---

## 5. 构建与部署链路【已运行，本轮全程验证】

```
python tools/check-xaml.py            # 39 个 XAML 良构性（WMC9997 前置拦截）
python tools/check-script-opts.py     # 16 scripts / 3 shadowed，script-opts 匹配
python tools/check-winrt-events.py    # 29 声明 / 21 有 raise / 17 有订阅
   ↓ build.ps1 -Configuration Release -Platform x64
C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe mpv-winrt.vcxproj   → mpv_winrt.dll
dotnet build mpv-winui.csproj                                     → 80 warning / 0 error
dotnet test mpv-conf-test                                          → 527 通过
dotnet test mpv-winrt-test                                         → 26 通过
   ↓ （产物）
mpv-winui/mpv-winui/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/
        mpvw.exe(483 KB) mpvw.dll(3.5 MB) mpvw.pri + 39 个 .xbf + 语言目录
   ↓ pwsh tools/deploy-test.ps1 -CloseRunning -Verify
C:\Users\z2637\Documents\mpv-project\mpv-deploy\   （overlay，不删目录；保留 WinAppSDK 运行时 DLL）
   ↓ pwsh tools/compare-deploy.ps1
identical 500 / differing 0 / 仅部署侧多 160（运行时 DLL + .mui）
```

**关键事实**
- 部署是 **overlay**，不是重建。【代码】`tools/deploy-test.ps1` 明确禁止 delete-and-recreate。
- `-CloseRunning` 会**先关进程再继续整套部署**，只想关窗口也会触发覆盖。【代码】同上
- 构建产物可直接运行，不必先进部署目录。【已运行】从 `bin\...\win-x64\mpvw.exe` 直接启动成功。
- 框架：`net10.0-windows10.0.26100.0`、`PublishAot=true`、`SelfContained=true`、`WindowsAppSDKSelfContained=true`、`AssemblyName=mpvw`。【代码】`mpv-winui.csproj:1-24`

---

## 6. 结构性问题（本轮实际发现）

| # | 问题 | 位置 | 证据 |
|---|---|---|---|
| S1 | **`playlists` 分区是死分支**：常量 + 5 处 switch 分支仍在，但无任何导航入口可达 | `MpvPlayerPage_Library.cs:70,425,534,543,552,563` | 【代码】 |
| S2 | **首页「打开文件」按钮被无条件隐藏**：方法叫 `UpdateShellOpenFileButton` 却恒设 `Collapsed`，方法名与行为相反 | `MainWindow.xaml.cs:712-731` | 【代码】 |
| S3 | **`MpvPlayerPage.xaml` 2418 行 + `MpvPlayerPage_Library.cs` 2962 行**，且库/播放/菜单/拖放全在一个 Page 内，是最大的耦合点 | 同上 | 【代码】 |
| S4 | **`Themes/Generic.xaml` 是空壳**（12 行 / 0 key），实际主题都在 `Styles.xaml` 与 `MainWindow.xaml` 内联；命名有误导性 | `Themes/Generic.xaml` | 【代码】 |
| S5 | **`tools/mvpw-mcp/` 60 个验证器从未接入 CI 或 dsh**；`verify_e2e.py` 手工跑才有效果 | `tools/mvpw-mcp/` | 【已运行】MCP 服务可启动、工具可列出，但 dsh 未配置 |
| S6 | **框架级技术债**：`PlayerTrackSelectorControl.xaml` 用 UWP 专属 `<Pivot>`（WUI2003 ×5，WinUI 3 无直接等价物）；`MediaInfoUserControl.xaml` 9 处 x:Bind 默认 OneTime（WUI2011）；`WMC1506` OneWay 绑定告警约 60 条 | 构建日志 | 【已运行】 |
| S7 | **`ShellOpenFileButton` / `LibraryOpenFileButton` / `VideoAddFolderButton` 三个"打开"入口并存**，靠 section 切换显隐，容易再次出现"两个打开按钮"的重复 | `MainWindow.xaml` + `MpvPlayerPage.xaml` | 【代码】 |
| S8 | **两份重复的云端副本**：`mpv\_cloud\mpv-winui-player`（2026-09-19 快照）、`Documents\DeepSeek\mpv-winui-player`（`ec5b324`，其提交全部已含于本仓库 `main`） | 磁盘 | 【代码】 |

---

## 7. 本基线**未**覆盖的部分

诚实列出，避免后续被当作已核对：

- 各功能在运行时的**逐项**可用性：本轮只做了 1 次端到端（`verify_e2e.py` 19/19）与 1 次 UIA 元素枚举，**没有**逐页面、逐控件做交互验证。功能状态表尚未产出。
- mpv 播放本身（真实媒体文件、音轨/字幕切换、全屏、PiP）未验证。
- 设置页 356 条生成项的实际渲染与生效，未验证。
- 高对比 / 亮暗主题，未验证。
- 云端 669 个提交的"历史整理"：只做了拓扑判定（本地是严格超集），**没有**逐条比对功能意图。这需要按主题域分批做，且需要你先确认 C-01。
