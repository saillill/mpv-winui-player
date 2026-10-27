# 怎么验证改动

构建、检查器、测试，以及**已知盲区**。先读 `§4`：有 2 个检查器是假绿，
20 个检查器不在任何构建链路上。

---

## 1. 构建

```powershell
.\build.ps1 -Configuration Release -Platform x64
```

参数：`-Configuration Debug|Release`（默认 `Debug`）、`-Platform x64|ARM64`（默认 `x64`）、
`-CheckLocalization`、`-FetchMpvLibs`。**用全名**，`-Release x64` 会静默退回 Debug。

| # | 步骤 | 行号 |
|---|---|---|
| 0 | 检查 `src\libs\bin\<P>\mpv-2.dll` 与 `lib\<P>\mpv.lib`；缺则 `-FetchMpvLibs` 下载，否则报错退出 | 22-33 |
| 1 | `check-xaml.py` | 37-39 |
| 2 | `check-script-opts.py`（读 `mpv-2.dll`，故必须在 libs 到位后） | 46-48 |
| 3 | `check-winrt-events.py`（纯源码） | 56-58 |
| 4 | `nuget restore` C++ 工程（仅当 `nuget` 在 PATH） | 66-73 |
| 5 | `MSBuild src\mpvw-native\mpv-winrt.vcxproj` | 75-77 |
| 6 | `dotnet build src\mpvw-app\mpv-winui.csproj` | 79-81 |
| 7 | `dotnet build tests\conf --no-incremental` → `dotnet test --no-build` | 117-120 |
| 8 | `dotnet build tests\native --no-incremental` → `dotnet test --no-build` | 131-134 |
| 9 | `check-localization.py` — **仅当 `-CheckLocalization`** | 138-139 |

`$ErrorActionPreference='Stop'`，每步后显式 `if ($LASTEXITCODE -ne 0) { exit }`，任一步失败即中止。

**预期产物**：`src\mpvw-app\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\mpvw.dll`。
程序集名是 `mpvw`，不是 `mpv-winui`。

**`--no-incremental` 不是冗余。** `build.ps1:105-106` 记录实测：改坏
`ControlBarAdaptiveLayout.cs` 但保留原 mtime，增量 `dotnet test` 报 **496/496 PASS**，
`--no-incremental` 报 **495/496 FAIL**。git checkout 和 `Copy-Item -Force` 都会把 mtime
设成检出时间，增量构建于是跳过重编译并复述上次结论。

---

## 2. 25 个检查器

总数：`(git ls-files 'tools/check-*.py' | Measure-Object).Count` → **25**。

| 由谁调用 | 数量 | 名单 |
|---|---:|---|
| `build.ps1` | 4 | `check-xaml`、`check-script-opts`、`check-winrt-events`、`check-localization`（`-CheckLocalization` 时） |
| CI | 5 | 上述 4 个 + `check-settings-drift` |
| **两边都不跑** | **20** | 其余 |

能跑全 25 个的只有 `tools/ui-repair/gate.py`，但它所在目录被 `.gitignore` 排除，
且未被任何 `.ps1` / `.yml` 调用（实测 0 处）。

| 检查器 | 比较的两样东西 | 链路 |
|---|---|---|
| `check-xaml` | 每个 `.xaml` 能否被 XML 解析 + 注释里是否含 `--`（WMC9997） | build 第一步 + CI |
| `check-script-opts` | `mpv-2.dll` 里的 `script-opt=<s>-<k>=` ↔ `data/scripts/<s>.lua` 的选项表 | build + CI |
| `check-winrt-events` | IDL 事件 × 原生 `m_*Event(` 抛出点 × C# 订阅，三者 join | build + CI |
| `check-localization` | `AppLang*.cs` 属性 ↔ 9 个 `Languages/*.json`；`menus.json` ↔ schema | build（可选）+ CI |
| `check-settings-drift` | `AppSettings` ↔ `ToCommand` ↔ 设置树 key，三方互查 | **仅 CI** |
| `check-conf-lines` | `MpvOptionNames` 每个 name ↔ `ToCommand` 分支 + `ConfigOnlyConfLine` | 手动 |
| `check-mpvconf-precedence` | `ToCommand` 键 ↔ `MpvOptionNames` 键（双向）↔ 真实属性 | 手动 |
| `check-conflict-rules` | `ComputeWarning/Visible/Enabled` 读到的键 ↔ `WarningDependencyKeys`（双向） | 手动 |
| `check-sections` | `SettingsSections` ↔ `AppLang` 的 `Section*`/`Categories`；`sectionMap` 覆盖率；`sectionOrder` 完整性 | 手动 |
| `check-empty-sections` | 每个分区实际归入的 option 数 ↔ 目录行 | 手动 |
| `check-section-icons` | 分区图标 codepoint 是否碰撞 | 手动 |
| `check-search-index` | `BuildSearchEntry` 引用的字段；`Rank*` 序；mpv 名覆盖率 | 手动 |
| `check-placeholders` | 每个 `OptionType.String` 行是否有 `Placeholder` 或是路径字段 | 手动 |
| `check-resources` | `{StaticResource X}` 的 key ↔ 全部 XAML `x:Key` ∪ WinUI `generic.xaml` | 手动 |
| `check-scroll-maxwidth` | 可横向滚动的 `ScrollViewer` 直接子元素是否带 `MaxWidth` | 手动 |
| `check-visual-state-layout` | `VisualState` 的 `<Setter>` ↔ 无负 margin / 不改对齐 | 手动 |
| `check-ui-tooltips` | 每个交互控件的 AutomationName 与 ToolTip | 手动 |
| `check-icon-standard` | `FontIcon` codepoint ↔ 它声明的字体（**见 §4.2，已失效**） | 手动 |
| `check-typography-standard` | 间距/字号字面量 ↔ `Styles.xaml` 令牌（**见 §4.2，已失效**） | 手动 |
| `check-more-glyph` | `MoreButton` 的 `Glyph` ↔ `&#xE712;` | 手动 |
| `check-baseline-lines` | baseline 每条的 `path:line:kind` 是否仍落在目标位置 | 手动 |
| `check-no-focus-steal` | 探针脚本里的前台 API 调用 ↔ 允许清单 | 手动 |
| `check-no-launch-activate` | `App.OnLaunched` 是否无条件 `Activate()`；`Start-Process` 是否带 `MVPW_NO_ACTIVATE` | 手动 |
| `check-doc-ranges` | 每条 `file:a-b` 的起止是否同一块（**只覆盖已删的旧文档**） | 手动 |
| `check-doc-references` | 文档里 `file:line` 是否存在（**同上**） | 手动 |

按改动类型该跑哪几个，见 [`settings-contract.md`](settings-contract.md) §5（带命令）。
通用判据：改 XAML 就跑 `check-xaml` + `check-resources` + `check-scroll-maxwidth`；
改设置就跑 `check-settings-drift` + `check-conf-lines` + `check-mpvconf-precedence`；
改分区就跑 `check-sections` + `check-empty-sections` + `check-section-icons` + `check-localization`；
**改检查器本身**要先注入真实违规确认它变红。

---

## 3. 两个测试套件

| 套件 | .cs | `[Test]` | `[TestCase]` | `[Explicit]` | 实跑 |
|---|---:|---:|---:|---:|---:|
| `tests/conf/mpv-conf-test` | 22 | 295 | 232 | 0 | **527** |
| `tests/native/mpv-winrt-test` | 3 | 22 | 6 | 1 fixture | **26** |

`dotnet test` 实测：`通过: 527` / `通过: 26`。`CoverArtProbe` 整个 fixture 标了
`[Explicit]` 且硬编码路径在本机不存在，28 个里恒跳 2 个。

**`mpv-conf-test` 测什么**：它用 `<Compile Include>` **链接**应用源码（16 条目 → 27 文件），
所以只覆盖不依赖 WinUI 的纯逻辑——`mpv.conf` 行级解析/写回（引号语义、`--` 前缀、
定长引号、CRLF/BOM、section 增删）、`Quote`/`Unquote` 双射与**重复写读不增长**
（对应那次 896 MB 缺陷）、`SettingValueCodec` 的 uint→bool、`menu.conf` round-trip、
`AtomicFile`、`FileService`、`MpvProtocolUri`、`LanguageCode`，以及纯函数
（控制栏档位、首字母归档、索引栏缩放、频谱、专辑主色、弹簧曲线）。

**不覆盖**：任何 XAML / 渲染；任何 Page code-behind；`PlayerControl`；`MainWindow`；
`ManagedMpvConfig`、`MpvConfWatcher`、`MpvConfOverrides`（被 `onBeforeSave` 解耦出去）；
`MpvSettings` 的 1422 行映射表；`AppSettings`；`Option*Control` 全族；主题/Mica；
任何 Windows 运行时互操作。

**`mpv-winrt-test` 测什么**：`ProjectReference` 投影工程，驱动**真** `MpvPlayer`——
初始化与音量回读、批内非法选项不中断且顺序保留、自造 WAV 下 `PositionChanged` 真触发、
`ObserveProperty` 注销后不回调、`SpeedChanged` 值、`ControlBarLayoutGrammar` 纯解析。

**不覆盖**：视频输出 / `SwapChainPanel` / `VoConfigured`、字幕、音轨、章节、播放列表、
PiP、HDR/VSR、MediaInfo 标签与封面、`mpv.conf` 优先级、任何 UI。

---

## 4. 验证盲区

### 4.1 大文件、零测试引用

`tests/` 下对 `MpvPlayerPage` / `SettingsPage` 的唯一命中是**一句注释**
（`MpvProtocolUriTests.cs:12`）。

| 区域 / 文件 | 行数 | 测试引用 |
|---|---:|---:|
| `MpvPlayerPage*.cs`（25 个） | **9657** | 0 |
| `SettingsPage*.cs`（17 个） | **17725** | 0（仅注释） |
| `PlayerControl.xaml.cs` | 2576 | 0 |
| `Lexicon/Language/AppLang.cs` | 2063 | 0 |
| `Domains/MpvConf/MpvSettings.cs` | 1422 | 0 |
| `Shell/MainWindow.xaml.cs` | 1103 | 0 |
| `Controls/OptionListControl.xaml.cs` | 1074 | 0 |
| `Infrastructure/Motion/MotionBehaviors.cs` | 876 | 0 |
| `Domains/MpvConf/ManagedMpvConfig.cs` | 712 | 0 |
| `Domains/MpvConf/MpvConfWatcher.cs` | 242 | 0 |

单文件最大的是 `MpvPlayerPage_Library.cs`（1472 行）。
`mpv-conf-test` 链接的 27 个文件约占应用代码的 5%。

### 4.2 两个基线检查器曾经是假绿（已修复）

`check-icon-standard.py` 与 `check-typography-standard.py` 曾都只遍历
`("Modules", "Shell")`，而 `src/mpvw-app/Modules` 早已改名为 `Domains`。实测后果：
39 个 XAML 里只看了 1 个，两个检查器都输出 `OK: 1 XAML file(s)`；
`icon-standard-baseline.txt` 的 49 条里 46 条、`typography-baseline.txt` 的 13 条全部
落在从不被扫描的 `Domains/`。

**已改为 glob 整个 `src/mpvw-app/`**（排除 `bin`/`obj`），这样目录改名不会再让它失明。
修复后立刻暴露出 17 + 3 条从未被检查过的既有发现；那 20 条是基线行号漂移
（基线记的是属性行、检查器报的是元素起始行），已用 `--update-baseline` 重新生成。
现在两边都是 `OK: 39 XAML file(s)`。

### 4.3 `check-empty-sections` 曾经恒 exit 0（已修复）

它把 `unresolved` / `empty` / `orphan` 三个发现打印出来，然后无条件 `return 0`，
所以不能作为闸门。现在三者任一非空即 `exit 1`。

`check-placeholders` 有真实的 `return 1` 失败路径，不是恒 0。

### 4.4 检查器的工作目录依赖（已修复）

五个检查器曾用相对路径定位源码，只有从仓库根运行才有效。最危险的是
`check-placeholders`：从 `C:\` 运行时它读到 0 个文件，仍打印
"OK: every non-path free-text setting shows an example" 并 `exit 0`。

全部改为从 `__file__` 推导绝对路径，并给 `check-placeholders` 加了
"匹配到 0 个文件即 `exit 2`" 的守卫。现在 25 个检查器从仓库根与从 `C:\`
运行结果一致（实测两处均 0 失败）。

### 4.5 其余既存问题

- `check-baseline-lines` 现在就有 1 条 stale（`icon-standard-baseline.txt:43`），exit 0。
  任何在 baseline 行之前增删行的编辑都会让后续行号漂移，需手工重算偏移。
- `tools/report-missing-options.py` **不可用**：`exit 1`，报
  `curated names that are not mpv options: ['cuda-decode-device']`（该名在
  `mpv-options.json` 里 0 次）。它产出的任何数字都不成立。
- `check-doc-references` / `check-doc-ranges` 的 `DOCS` 列表只含本次删除的旧文档；
  它们对缺失文件有 `os.path.exists` 保护，所以既不会红也不会检查新文档。
  **新文档的交叉引用没有自动校验。**

### 4.6 没有 UI 层测试

控件模板、`VisualState`、`x:Bind`、焦点、键盘导航、DPI、高对比、Mica/Acrylic
全部无自动化覆盖。`check-visual-state-layout` 与 `check-ui-tooltips` 只做静态文本检查。

---

## 5. CI 与本地链路的差异

| 项 | `build.ps1` | CI |
|---|---|---|
| 调用 `build.ps1` | — | **不调用**，自己重排步骤 |
| 默认配置 | Debug | Release |
| `check-settings-drift` | 不跑 | **跑** |
| `gen-schema-options.py --check` | 不跑 | **跑** |
| `check-localization` | 可选 | **必跑** |
| `--no-incremental` | 有 | **无** |

本地过、CI 红是可能的（`check-settings-drift` 与生成器新鲜度只在 CI 跑）。
CI 过也不等于 25 个检查器都过——两边并集只有 5 个。

**加检查器的三条规则**（`AGENTS.md` 与各 docstring 的共识）：
① 先看它红——注入**真实**违规，不是合成违规；② 拒绝不可信输入——
"解析到 0 项"必须是错误而不是健康，否则停止匹配的正则会永远报成功；
③ 问代码读什么，不要问注释说它读什么——曾有 6 个检查器在测量树的错误一半时保持绿色。

---

## 6. 重算本文每个数字

```powershell
Select-String -Path build.ps1 -Pattern 'check-|MSBuild|dotnet build|dotnet test|Write-Host "==>'

(git ls-files 'tools/check-*.py' | Measure-Object).Count                    # 25
Select-String -Path build.ps1 -Pattern 'check-[\w-]+\.py' -AllMatches | % { $_.Matches.Value } | Sort-Object -Unique   # 4
Select-String -Path .github/workflows/build.yml -Pattern 'check-[\w-]+\.py' -AllMatches | % { $_.Matches.Value } | Sort-Object -Unique   # 5
(Get-ChildItem -Recurse -File -Include *.ps1,*.yml | Select-String 'gate\.py').Count   # 0

foreach ($s in @('conf','native')) { foreach ($p in @('\[Test\]','\[TestCase','\[Explicit')) {
  "{0,-7}{1,-12}{2}" -f $s,$p,((Select-String -Path "tests\$s\*.cs" -Pattern $p -AllMatches | % { $_.Matches.Count } | Measure-Object -Sum).Sum) } }
dotnet test tests\conf\mpv-conf-test.csproj -c Release                       # 527
dotnet test tests\native\mpv-winrt-test.csproj -c Release -p:Platform=x64    # 26

# 两个假绿（§4.2）：扫描根写死 Modules，该目录已不存在
Select-String -Path tools\check-icon-standard.py, tools\check-typography-standard.py -Pattern '"Modules", "Shell"'
Test-Path src\mpvw-app\Modules                                               # False
python tools\check-icon-standard.py --verbose        # OK: 1 XAML file(s)
(Get-Content tools\icon-standard-baseline.txt | ? { $_ -match 'Domains' }).Count   # 46（共 49）
(Get-Content tools\typography-baseline.txt   | ? { $_ -match 'Domains' }).Count   # 13（共 13）
Select-String -Path tools\check-placeholders.py, tools\check-empty-sections.py -Pattern 'return 0'

# 盲区行数
Get-ChildItem src\mpvw-app\Domains\Playback -Filter 'MpvPlayerPage*.cs' | % { [System.IO.File]::ReadAllLines($_.FullName).Count } | Measure-Object -Sum   # 9657
Get-ChildItem src\mpvw-app\Domains\Settings -Filter 'SettingsPage*.cs'  | % { [System.IO.File]::ReadAllLines($_.FullName).Count } | Measure-Object -Sum   # 17725
Select-String -Path tests\conf\*.cs, tests\native\*.cs -Pattern 'MpvPlayerPage|SettingsPage'   # 1 条注释
Select-String -Path tests\conf\mpv-conf-test.csproj -Pattern 'Compile Include=' | Measure-Object   # 16
```

---

## 7. 原因不明

- **为什么 20 个检查器不在任何链路上**：`gate.py` 能跑全 25 个，但它所在目录被
  `.gitignore` 排除、也没有任何 `.ps1`/`.yml` 调它。是"只在需要时手动跑"的设计，
  还是流水线遗漏，仓库里没有说明。
- **两个检查器的扫描根为什么没跟着 `Modules` → `Domains` 改名更新**：`c36e1d9`
  修过 `check-xaml.py` 与 `check-script-opts.py` 的根路径（提交说明第二条），
  这两处当时就没改。是遗漏还是刻意保留，无法确认。
