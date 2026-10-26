# ISSUE_LOG — 问题与结论台账

> 规则：每条结论标注证据等级。
> - 【已运行】本轮实际运行程序/UIA 读取确认
> - 【代码】从源码或 git 对象读出的静态事实，带 `路径:行号` 或 commit
> - 【推断】由代码推断，未运行
> - 【未知】无法判断，需要补充信息
>
> 本文件只记录本轮**亲自验证**的内容。不引用项目内其他文档的既有说法。

基线时间：2026-10-26 20:45–21:05（Asia/Shanghai）
基线 HEAD：`0d0e889` `ui: returning to a playing track restores the music page`

---

## A. 环境与链路基线

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| A1 | .NET SDK | 8.0.405 / 10.0.204 / 10.0.402；默认 10.0.402 | 【已运行】`dotnet --list-sdks` |
| A2 | MSBuild | VS Build Tools 17.14.51.32402，位于 `C:\BuildTools\MSBuild\Current\Bin\MSBuild.exe`（build.ps1 硬编码该路径） | 【已运行】`MSBuild -version` |
| A3 | Windows SDK | 10.0.22621.0、10.0.26100.0 均在 `C:\Program Files (x86)\Windows Kits\10\References` | 【已运行】 |
| A4 | WindowsAppRuntime | 已装 1.3–1.8 与 2.3/2.4/2.5 多版本 | 【已运行】`Get-AppxPackage` |
| A5 | mpv 原生库 | `mpv-winui\libs\bin\x64\mpv-2.dll`（63,013,888 B）+ `mediainfo.dll` 存在；该目录被 gitignore | 【已运行】目录列举 |
| A6 | NuGet 包 | `mpv-winui\packages\` 已含 cppwinrt / WinAppSDK / WebView2；`nuget.exe` 不在 PATH，build.ps1 走 packages 回退分支 | 【已运行】 |
| A7 | Python | 3.14.4，`mcp`、`uiautomation`、`PIL`、`psutil` 可导入；`pywinauto` 缺失 | 【已运行】`importlib.util.find_spec` |
| A8 | winapp CLI | `0.7.1`，位于 `%LOCALAPPDATA%\Microsoft\WindowsApps\winapp.exe`，`winapp ui` 全套 UIA 子命令可用 | 【已运行】`winapp ui inspect -a mpvw` |

## B. 构建与部署

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| B1 | Release x64 构建 | **通过**。0 error；app 78 warning；mpv-conf-test 2 warning；mpv-winrt-test 2 warning（NU1603 版本回退） | 【已运行】`build.ps1 -Configuration Release -Platform x64`，exit 0 |
| B2 | 测试 | mpv-conf-test 527 通过 / 0 失败；mpv-winrt-test 26 通过 / 0 失败（2 个 CoverArt 用例 skip 后仍计入通过） | 【已运行】 |
| B3 | 构建门禁 | build.ps1 内置 3 个前置检查全部通过：check-xaml（39 文件）、check-script-opts、check-winrt-events | 【已运行】 |
| B4 | 构建产物位置 | `mpv-winui\mpv-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\mpvw.{exe,dll,pri}` | 【已运行】 |
| B5 | 部署同步（修复前） | **不一致**：部署目录 `mpvw.dll` 为 19:31:47 版本，新构建为 20:47:54；SHA256 不同 | 【已运行】`Get-FileHash` 比对 |
| B6 | 部署同步（修复后） | `deploy-test.ps1 -CloseRunning` 覆盖 500 个文件后：identical 500 / differing 0 / 仅部署侧多 160 个（WinAppSDK 运行时 DLL 与 .mui）/ xbf 39=39 | 【已运行】`compare-deploy.ps1` |
| B7 | 运行时可用性 | 从 `mpv-deploy` 启动成功，窗口标题 `MPV`，响应正常，UIA 可读 32 个可交互元素 | 【已运行】 |
| B8 | 进程收尾 | 用 `deploy-test.ps1 -CloseRunning`（WM_CLOSE 优先）关闭，确认无残留 mpvw 进程 | 【已运行】 |

**注意（B9）**：`deploy-test.ps1 -CloseRunning` 会**先关闭再继续部署**，即使只想关闭也会执行整套复制。本轮就是因此顺带完成了 B6。单独关窗口请用 `CloseMainWindow` 或 `deploy-test.ps1 -CloseRunning` 并接受覆盖。

## C. Git 拓扑（与任务描述的关键分歧）

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| C1 | 本地 `main` | 732 commits，HEAD `0d0e889` | 【代码】`git rev-list --count main` |
| C2 | 远端 `cloud/main` | 669 commits，HEAD `bf8e302`（2026-10-24 03:20） | 【代码】fetch 后实测 |
| C3 | 领先/落后 | **本地领先 63，落后 0**。`cloud/main` 是 `main` 的祖先（fast-forward 关系），无分叉 | 【代码】`rev-list --left-right --count main...cloud/main` → `63 0`；`merge-base --is-ancestor` exit 0 |
| C4 | `cloud/upstream-merge` | 490 commits，**已完全包含在 `cloud/main` 内**，对 `main` 无独有提交 | 【代码】`rev-list --count main..cloud/upstream-merge` → 0 |
| C5 | 结论 | 云端不包含任何本地没有的内容。**以云端为溯源重建会丢掉本地 63 个提交** | 【代码】C3+C4 |

## D. 63 个本地提交的实际影响面

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| D1 | 总变更量 | 106 文件，+9740 / −552 | 【代码】`git diff --shortstat cloud/main..main` |
| D2 | 源码目录净变化 | `mpv-winui/` 下 43 文件，+1483 / −270，**净增** | 【代码】`git diff --stat cloud/main..main -- mpv-winui/mpv-winui/Modules/` |
| D3 | 文件删除 | **`mpv-winui/` 下没有任何文件被删除**（`--diff-filter=D` 为空） | 【代码】 |
| D4 | 新增文件 | `Modules/Player/IndexRailHover.cs`、`Modules/Player/PlayChain.cs`、`Modules/Settings/SettingValueCodec.cs` | 【代码】`--name-status` 中 A 项 |
| D5 | Shell 层唯一改动 | 只有 `Shell/MainWindow.xaml.cs` 变化（+87 / −7）；`Shell/MainWindow.xaml` **完全未变** | 【代码】`git diff --stat cloud/main..main -- Shell/` |
| D6 | 提交性质分布 | 63 中：纯文档约 19、纯工具/门禁约 18、含代码约 26 | 【代码】逐提交 `--name-only` 分类 |

## E. 已发现的问题与风险

| ID | 严重度 | 问题 | 证据 | 状态 |
|---|---|---|---|---|
| I-01 | 高 | **任务前提与仓库事实相反**：不是"工作区与云端没对齐"，而是本地领先云端 63 个提交。任何"从云端重建"的方案都会回退 63 个提交的工作 | C3 | 待用户裁决 |
| I-02 | 高 | 部署产物曾落后于源码（19:31 vs 20:47 构建）。用户正在运行的 mpvw 不是最新代码 | B5 | 已修复（B6） |
| I-03 | 中 | 首页「打开文件」入口被**主动隐藏**：`UpdateShellOpenFileButton` 现在无条件 `Visibility.Collapsed`，理由是与媒体库标题行的按钮重复 | 【代码】`Shell/MainWindow.xaml.cs:712-731`（提交 `1b49ab9`） | 待确认是否为"丢失的功能" |
| I-04 | 中 | `LibrarySectionPlaylists` 常量仍存在并参与 5 处 switch 分支，但**没有任何导航入口**能到达；导航项在 `0b17cc0` 起就只有 home/music/videos/queue 四项，云端同样如此 | 【代码】`MainWindow.xaml.cs:187-190`；`MpvPlayerPage_Library.cs:70,425,534,543,552,563` | 死分支，待确认是否删 |
| I-05 | 中 | `tools/mvpw-mcp/` 这个 MCP 服务器**从未在 dsh 中注册**，且 `mcpw_server.py` 的注释只给了 claude_desktop_config 的写法 | 【代码】`tools/mvpw-mcp/mcpw_server.py:1-24`；dsh profile 中无 mcpServers | 待接入 |
| I-06 | 低 | 三处重复代码副本：`mpv\_cloud\mpv-winui-player`（gitignore，9-19 快照）、`Documents\DeepSeek\mpv-winui-player`（`ec5b324`，其提交已全部包含在本仓库 `main` 中，工作区干净） | 【代码】`merge-base --is-ancestor ec5b324 main` exit 0 | 可安全清理 |
| I-07 | 低 | `mpv-deploy\native\mpvw.exe`（22,683,136 B，2026-10-09，FileVersion 1.0.0.0）来源不明，与当前 483 KB 的 launcher 完全不同，疑似旧自包含单体构建残留 | 【运行】文件属性 | 待确认 |
| I-08 | 低 | 构建告警：`PlayerTrackSelectorControl.xaml` 使用 UWP 专属 `<Pivot>`（WUI2003 ×5）；`MediaInfoUserControl.xaml` 多处 x:Bind 默认 OneTime（WUI2011 ×9）；`WMC1506` OneWay 绑定告警约 60 条 | 【已运行】构建日志 | 技术债，不阻塞 |
| I-09 | 低 | 旧工作区 `D:\Workspace\WorkBuddy\MPV`：498.5 MB / 2360 文件，最后一次写入 2026-10-21；无锁、无进程占用、无 mpvw/dotnet 进程引用；仅含 `.workbuddy\memory`、`.workbuddy\tmp`（约 2000 个一次性 Python 脚本）、`docs`、`archive` | 【已运行】文件锁测试 + `Win32_Process` 扫描 | 可安全归档 |

## G. 洁净重建验证（本轮完成）

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| G1 | 洁净克隆 | 从本地 `main` `git clone --no-hardlinks` 到独立目录，工作树干净（0 改动），HEAD `0d0e889` | 【已运行】 |
| G2 | 补齐被忽略的原生依赖 | 复制 `mpv-winui\libs`（68 文件）与 `mpv-winui\packages`（1588 文件）后即可构建 | 【已运行】 |
| G3 | 洁净构建 | **完全通过**：0 error、80 warning、mpv-conf-test 527 通过、mpv-winrt-test 26 通过 | 【已运行】`build.ps1` exit 0 |
| G4 | 直接运行构建输出 | 不经部署目录，直接启动 `bin\x64\Release\...\win-x64\mpvw.exe`，窗口标题 `MPV`，响应正常 | 【已运行】 |
| G5 | 结论 | **仓库本身是可重建的**，不依赖被 gitignore 之外的任何隐藏状态；重建唯一必需的额外输入是 `libs\`（mpv-2.dll 等，可从 `tools/fetch-mpv-libs.ps1` 重新下载）与 `packages\`（nuget restore 可重新生成） | 【已运行】G1–G4 |
| G6 | 验证目录已清理 | `mpv-rebuild-verify` 已删除，磁盘回到基线 | 【已运行】 |

## H. 死代码与丢失内容核查（针对 git dangling 对象）

`git fsck --lost-found` 发现 23 个悬挂提交。逐个核查后分类：

| 类型 | 数量 | 结论 |
|---|---|---|
| stash 提交（3 父） | 5 | 是历史 stash 的残留，非丢失工作 |
| stash 的 WIP 索引提交（2 父，`WIP on main:`） | 10 | 同上 |
| 真实孤立提交 | 8 | 见下表 |

| 提交 | 主题 | 是否已在 `main` | 判定 |
|---|---|---|---|
| `87a638c` | Tooling: fluent-icon generator… | **是**（patch-id 命中 `a285d19`） | 已落地，重复 |
| `96687d3` | check-section-icons: name the glyph correctly… | **是**（`a9b5c71`） | 已落地，重复 |
| `b4ff0fa` | Music: real cover art and tags… | **是**（`3017c3c`） | 已落地，重复 |
| `be1b4c6` | update package version | 否 | **孤立根提交**（无父），内容是一份早期全量树，与现仓库无关 |
| `375d615` | Breadcrumb: drop the Button, not the offset | 否（patch-id） | 但**结果已在码中**：`MpvBreadcrumbSectionNudge` 现值 `0,0,0,0`（`Styles.xaml:1647`），正是该提交想要的终态；差异只是注释文字 |
| `bede606` | Music: rhythm strip → spectrum + cover colour | 否（patch-id） | 但**功能已在码中**：`Modules/Player/Audio/{SpectrumAnalyzer,AudioCapture,AlbumColorExtractor,ArtworkPalette}.cs` 全部存在，且 `SpectrumAnalyzerTests.cs`、`AlbumColorExtractorTests.cs` 在测试套件内并通过 |
| `45723df` | Music: cover lost because tag pass replaced cards | 否（patch-id） | 但**结果已在码中**：`MpvPlayerPage_Library.cs` 有 57 处 thumbnail 处理 |
| `c537bd9` | "On main: wip-mica-probe"（stash） | — | Mica 探针的实验残留，108 文件近乎全量替换，是快照而非工作 |

**H 结论**：未发现"被覆盖或删除的、尚未在现码中恢复"的功能。所有能被追溯到的功能提交，其结果要么已在 `main`，要么已被后续提交以不同形式重做。

---

## I. 完整门禁（本轮实跑）

`python tools/ui-repair/gate.py` —— **16/16 阶段全部通过**，每个行为验证器都自带 `--self-test` 证明自己能变红【已运行】：

| 阶段 | 结果 |
|---|---|
| check-xaml | PASS：39 个 XAML 良构 |
| build | PASS：exit 0，0 error |
| static checkers | PASS：25/25 |
| deploy consistency | PASS：differing=0 |
| verify_playback | PASS + self-test 可红 |
| verify_e2e | PASS + self-test 可红 |
| verify_settings / verify_settings_rows | PASS ×2 + self-test 可红 |
| verify_screenshot | PASS + self-test 可红 |
| verify_music_cards | PASS + self-test 可红 |
| verify_video_folder_cover | PASS + self-test 可红 |
| verify_card_overlay | PASS + self-test 可红 |
| verify_letter_rail | PASS + self-test 可红 |
| verify_queue_button | PASS + self-test 可红 |
| verify_home_button | PASS + self-test 可红 |
| verify_overlay_buttons | PASS + self-test 可红 |

**这条对本任务的意义**：仓库当前的 9 个具名 UI 缺陷（专辑/艺术家封面、视频文件夹封面、卡片蒙层、字母栏、队列按钮、首页按钮、蒙层按钮遮挡）在部署版上**全部实测通过**，不是文档声称，是本轮跑出来的。收尾确认无残留 mpvw 进程，部署 `identical 500 / differing 0`。

---

## F. 待用户确认

| ID | 问题 |
|---|---|
| C-01 | 云端角色：本地领先 63 个提交，"以云端为溯源"是否改为"以本地 `main` 为真相源，云端仅作备份目标"？是否允许把这 63 个提交 push 到 `cloud/main`（fast-forward，无冲突）？ |
| C-02 | I-03：首页隐藏的「打开文件」按钮，是"被删掉的既有功能"要恢复，还是认可当时去重的判断？ |
| C-03 | I-04：`playlists` 分区的死代码，删除还是保留？ |
| C-04 | "删除过时部署与历史"具体指哪些？`mpv-deploy` 整目录？`mpv-deploy\native\`？旧工作区 `D:\Workspace\WorkBuddy\MPV` 是删除还是归档？ |
| C-05 | `docs/` 下 30+ 份既有文档（含 `docs/ui-repair/` 全套）如何处理：保留、归档到 `docs/archive/`，还是删除？ |
| C-06 | 重建目录的落点：G1–G5 已证明**仓库本身无需重建**。是否改为"在现有仓库内清理 + 建立 `docs/alignment/`"，而不是另起空目录？ |
| C-07 | I-05：是否授权我把 `tools/mvpw-mcp/mcpw_server.py` 注册为 dsh 的 MCP 服务器（需改 `~/.dsh/profiles/desktop/cordis.patch.yml`，加一条 `dsh-mcp-client` 配置）？ |
| C-08 | I-07：`mpv-deploy\native\mpvw.exe` 是否删除？ |

---

## 变更记录

| 时间 | 内容 |
|---|---|
| 2026-10-26 21:05 | 建立本文件；记录环境、构建、部署、Git 拓扑基线；登记 I-01～I-09 与 C-01～C-08 |
| 2026-10-26 21:25 | 补 G 节（洁净重建验证：克隆→构建→运行全部通过）与 H 节（23 个 dangling 提交逐个核查，未发现丢失功能） |
