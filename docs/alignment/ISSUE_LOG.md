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

---

## J. 本轮（2026-10-26 21:00 起）—— 重建执行与实测

> 本节只记本轮亲自跑出来的内容。证据等级沿用文件头定义。

### J1 阶段 A · 提交并推送云端

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| J1-1 | 两个未跟踪项的处理 | `docs/alignment/` 三份 → 提交；`probe_surface_after_navigation.py` → 读后判定为**有效验证器**（依赖 `background_window`/`mpvw_tools`/`probe_hover_input`/`ui_measure`/`verify_card_overlay` 全部可导入，11 个属性全部存在），一并提交 | 【已运行】`ast` 依赖解析 + `importlib` 逐个导入 + `hasattr` 探针 |
| J1-2 | 推送前门禁 | `[DBG-` 在源码中 0 处（仅 `AGENTS.md:60` 与 `settings-design-contract.md:724` 的文档提及）；`git diff --check` exit 0；BOM 无变化；只 stage 了 4 个仓库文件 | 【已运行】 |
| J1-3 | 推送结果 | `bf8e302..0f70f74 main -> main`；`rev-list --left-right --count main...cloud/main` = **`0	0`**；`cloud/main` = `0f70f74`；733/733 | 【已运行】 |

### J2 阶段 C/D · 空目录重建与门禁

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| J2-1 | 克隆 | `mpv-clean` HEAD `0f70f74`，`git status --porcelain` 为空；补 `libs`（68 文件）+ `packages`（1588 文件）后仍为空 | 【已运行】 |
| J2-2 | 构建数字 | **0 error / 80 warning**（app）/ mpv-conf-test 527 / mpv-winrt-test 26。与基线完全一致 | 【已运行】`build.ps1 -Configuration Release -Platform x64` exit 0 |
| J2-3 | 构建产物文件数 | 502（含 2 个 .pdb），非 pdb 500 | 【已运行】 |
| J2-4 | 产物直接启动 | 标题 `MPV`；导航 4 项读作「主页/音乐库/视频库/播放队列」；播放条 3 项读作「播放/静音/全屏」；72 个 UIA 元素；进程关闭 | 【已运行】自写 `verify_exe.py`（UIA，非截图） |
| J2-5 | 门禁 | **16/16 阶段** | 【已运行】`python tools/ui-repair/gate.py` |
| J2-6 | 警告数 80 vs 78 | 增量构建报 78，`--no-incremental` 报 **80**。差异来自增量跳过未变文件，不是代码变化 | 【已运行】 |

### J3 阶段 E · 部署目录重建（瘦身）

| ID | 项目 | 结论 | 证据 |
|---|---|---|---|
| J3-1 | 重建前 | 660 文件 / 356.8 MB | 【已运行】 |
| J3-2 | 只在部署侧的 160 个，按类 | **language-dir 154**（77 个语言目录下的 `.mui`，3.0 MB）｜**dotnet-runtime 5**（1.9 MB，含 `mscordaccore_amd64_amd64_10.0.826.23019.dll` 与 `pt-BR\System.Windows.Forms*.resources.dll`）｜**native 1**（`native\mpvw.exe` 22,683,136 B = 21.6 MB）｜other **0** | 【已运行】`classify_deploy.py`，明细见 `deploy_only_files.json` |
| J3-3 | 与任务书描述不符之处 | 任务书称这 160 个含 `coreclr.dll` / `System.Private.CoreLib.dll` / `System.Windows.Forms.dll` / `AppxManifest.xml` / `mpv-winui.build.appxrecipe`。**实测这些文件在构建产物里也存在**，因此不属于"只在部署侧"。真正只在部署侧的是上面三类 | 【已运行】逐路径集合差 |
| J3-4 | 备份 | `mpv-deploy` → `mpv-deploy.bak-20261026`（660 文件 / 356.8 MB），改名前断言无 mpvw 进程。**本轮不删** | 【已运行】 |
| J3-5 | 重建 | 一次性复制构建产物 → 排除 2 个 `.pdb` → **500 文件 / 330.2 MB** | 【已运行】 |
| J3-6 | 一致性 | **`identical 500` / `differing 0` / `present only in dst 0`** | 【已运行】`compare-deploy.ps1` |
| J3-7 | 能启动 | 从 `mpv-deploy\mpvw.exe` 启动，标题 `MPV`，进程关闭 | 【已运行】 |
| J3-8 | **中文不退化（任务书点名的最大风险）** | **未退化**。移除 154 个语言 `.mui` 后，UIA 仍读到「主页/音乐库/视频库/播放队列」与「播放/静音/全屏」，与重建前逐项相同 | 【已运行】UIA 断言，非截图 |
| J3-9 | 门禁复跑（针对重建后的部署目录） | **16/16** | 【已运行】 |

### J4 阶段 F · 清理

| ID | 对象 | 结论 | 证据 |
|---|---|---|---|
| J4-1 | `D:\Workspace\WorkBuddy\MPV`（498.5 MB / 2360 文件） | **已归档** → `MPV.archived-20261026` | 【已运行】 |
| J4-2 | `Documents\DeepSeek\mpv-winui-player`（1657.5 MB / 5238 文件） | **已删除**。删前核实：HEAD `ec5b324`，工作树 0 改动，`merge-base --is-ancestor ec5b324 main` exit 0 | 【已运行】 |
| J4-3 | `mpv\_cloud`（49.1 MB / 505 文件） | **已删除** | 【已运行】 |
| J4-4 | 旧仓库 `mpv` → `mpv.old-20261026` | **改名失败，未达成**。见 J5 | 【已运行】 |
| J4-5 | 旧仓库内容去向（替代方案） | 除 `docs` 外全部顶层项已移入 `mpv.old-20261026`（2281 MB / 8047 文件，git HEAD `c90ca0e` 完好、可回滚）。原 `mpv` 现在只剩 `docs` 一个目录，0.70 MB / 39 文件 | 【已运行】 |
| J4-6 | `docs` 内容是否丢失 | **没有**。旧壳里 39 个文件在新仓库 `mpv-clean\docs` 中全部存在且被 git 跟踪（同 commit），归档里另存一份 | 【已运行】逐文件比对 |

### J5 未达成项：旧仓库目录无法改名（新发现）

| ID | 严重度 | 问题 | 证据 | 状态 |
|---|---|---|---|---|
| I-10 | 低（不阻塞交付） | 对 `C:\Users\z2637\Documents\DeepSeek\mpv` 改名返回 `Access to the path is denied`，而：父目录可写可建删、同级 12/13 个顶层子目录可改名、`docs` 内部文件与子目录可建可改名可删除、`docs` 的 ACL 与可改名兄弟目录**完全相同**（`z2637` FullControl、无 Deny 项）、`docs` 可被 `CreateFileW` 以 `DELETE|share-all` 打开、`RemoveDirectoryW(docs)` 返回 **err=145（目录非空）而非 err=5**，说明权限本身没问题。已排除：进程 cwd（343 个进程 PEB 逐个读 `CurrentDirectory`，无命中）、`.lock` 文件（无）、`Everything` 索引服务（停服务后仍失败）、`cmd ren`、`MoveFileExW`、短路径 | 【已运行】`who_locks_docs.py` / `hunt_lock.py` / ACL 对比 / `[F]::Try` DELETE 探针 | **未解决**；已用等价方案（J4-5）达成瘦身目的，`mpv` 名保留。需用户裁决是否强删 |

### J6 任务三 · 注释与代码一致性（只改注释）

| ID | 位置 | 注释原话 | 实测 | 处置 |
|---|---|---|---|---|
| J6-1 | `Shell/MainWindow.xaml.cs` `UpdateShellOpenFileButton` | 「the one place the shell's open-file button is shown or hidden」，条件为「Home AND not playing AND not full-window AND Home is not empty」 | 实为一条无条件 `Collapsed`；`_homeEmpty` 只写不读；且**不是唯一写入者**——`ChangeFullWindow` 也写该属性两次，全窗往返后按钮会重新 `Visible`（该行 blame 到 `0b17cc0`，早于 `1b49ab9`，未被其更新） | 【已改注释】行为未动 |
| J6-2 | 同上 `_homeEmpty` 字段 | 「见 UpdateShellOpenFileButton」 | 该 flag 不参与任何判断 | 【已改注释】字段保留（删除需连带删除事件订阅） |
| J6-3 | `Styles/Styles.xaml` `MpvShellAppIconMargin` | 声称让图标中心距 rail 边缘 20 DIP（48+20=68） | token 值 **10**；同一 StackPanel 里图标前有两个 48 宽按钮，故中心 = 96+10+10 = **116 DIP** | 【已改注释】token 值未动 |
| J6-4 | `Styles/Styles.xaml` `MpvNavPaneFooterIconMargin` | 4+4+8+8=24 使页脚图标中心落在 rail/2 | **成立**：token 供 8，另两个 4 来自按钮自身（`MainWindow.xaml:318` 同口径） | 【核对通过】未改 |
| J6-5 | `Shell/MainWindow.xaml` `PaneDisplayMode` 说明块 | 320 pane / 760 / 1120 阈值 / 自绘 toggle / 展开时无折叠入口 | **全部成立**（`MpvNavPaneWidth=320`，`CompactModeThresholdWidth=760`，`ExpandedModeThresholdWidth=1120`，`IsPaneToggleButtonVisible=False` + `ShellPaneToggleButton` 在扩张态 `Collapsed`） | 【核对通过】未改 |
| J6-6 | `Modules/Player/MpvPlayerPage_Library.cs:470-482` | Home 复用媒体库标题行 slot，shell 按钮因此隐藏 | **成立**：UIA 实测 Home 上 `VideoAddFolderButton` 的 name = 「打开文件」 | 【核对通过】未改 |
| J6-7 | 改注释时的自伤 | 第一版在 XAML 注释里写了两个连字符 | `check-xaml.py` 立即报 **WMC9997**（`Styles.xaml:1483`），构建前拦住 | 【已修正】 |

### J7 本轮发现并修复的工具缺陷（非产品缺陷）

| ID | 缺陷 | 为什么危险 | 证据 | 状态 |
|---|---|---|---|---|
| I-11 | **`tools/ui-repair/gate.py` 在新克隆里指向旧仓库**：`REPO = r"...\DeepSeek\mpv"` 硬编码，`run()` 默认 `cwd=REPO`。`mpv-clean` 里的 gate 会把 16 个阶段全部在**旧树**上执行 | 在错误的树上报 16/16，比失败更坏 | 【已运行】解析该文件求值，`REPO` 解析到 `...\mpv` | **已修复**：66 个文件改为从 `__file__` 推导；`mpv-deploy` 保持绝对路径。逐条求值核验 263 个赋值 / 78 文件，0 个指向旧树、0 个残留 |
| I-12 | **`gate.py` 丢弃失败证据**：`return False, "verifier says FAIL"`，不打印验证器自己的 FAIL 行 | 门禁说"失败"但说不出"看到了什么"，只能靠手工重跑——而重跑是另一次测量 | 【已运行】本次实测正是如此 | **已修复**：改为输出验证器首条 FAIL 行 + 条目数 |
| I-13 | **17 个探针脚本从 `D:\Workspace\WorkBuddy\MPV` 加载素材**，该工作区按阶段 F 被归档 | 归档后 `verify_screenshot` 稳定 3/3 失败（`error running command` → 0 字节文件），门禁卡在第 9 阶段 | 【已运行】3/3 复现 | **已修复**：4 个素材（243 KB）移入 `tools/mvpw-mcp/fixtures/`（哈希逐一比对），路径改为从 `__file__` 推导；17/17 求值通过 |
| I-14 | 素材路径的深度计算我连错三次（仓库根 → `<repo>\tools\tools\...` → 正确） | 只用"读代码"看不出来，必须**求值** | 【已运行】每次都是求值发现的 | 已在注释与提交信息中记录 |

### J8 本轮新增与变化的结论

- **变化**：任务书预期"部署目录 160 个只在部署侧文件 = .NET 运行时 + 90 个语言目录 + native/mpvw.exe + AppxManifest + appxrecipe"。**实测分类不同**（见 J3-2/J3-3）：语言目录是 154 个 `.mui` 分布在 77 个目录；运行时残留只有 5 个；`coreclr.dll`、`AppxManifest.xml`、`appxrecipe` 在构建产物中也存在，不属于"只在部署侧"。
- **变化**：任务书 I-07 记 `mpv-deploy\native\mpvw.exe` 来源不明——本轮它随 160 个一并清除，已不再是部署目录的一部分。
- **不变**：I-03（首页打开文件按钮）仍是**行为**上的待裁决项，但本轮拿到了新证据：Home 上确有读作「打开文件」的按钮（UIA），所以 shell 那个按钮属于残留而非缺失功能。判定仍留给用户。
- **不变**：I-04（`playlists` 死代码）、C-02、C-03 等仍需用户裁决，本轮未动。

### J9 本轮未验证 / 不确定

- 旧仓库改名失败的确切持有者：**未知**。已排除 cwd、`.lock`、Everything 服务、ACL 与 Deny、DELETE 权限、cmd/Win32/短路径三种调用方式。不排除是文件系统过滤驱动或某后台扫描器，未进一步定位。
- `verify_playback` 的"画面在动"分支：本机 composition 输出 + ImageGrab 恒为静态（`max frame-to-frame change 0.0%`），该验证器实际走的是"mpv 时钟前进"这条后备判据。像素分支**本轮未被真正验证**。
- `gate.py` 的 12 个验证器在本轮通过，但其中多个依赖 UIA 与本地媒体；它们的覆盖范围不等于"功能全部正确"。
- 首页/媒体库的逐项功能状态：本轮按任务要求**未做**功能排查。

| 时间 | 内容 |
|---|---|
| 2026-10-27 01:00 | 补 J 节：阶段 A/C/D/E/F 实测数值、部署目录 160 个文件的分类、注释核对 7 条、工具缺陷 I-11～I-14、未达成项 I-10、本轮未验证清单 |