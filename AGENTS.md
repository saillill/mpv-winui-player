# mpv-winui-player — 给改代码的人

WinUI 3 播放器，宿主 libmpv。三部分：C# WinUI 应用（`src/mpvw-app/`）、
C++/WinRT 组件（`src/mpvw-native/`）、配置层（`data/`）。LGPL-2.1。

**本文只放"进来先知道什么"。** 深入内容各有权威文档，下面按"要改什么"给出入口。

---

## 目录

```
src/mpvw-app/          C# 应用（WinUI 3，net10.0-windows10.0.26100.0）
  App.xaml(.cs)        入口，必须留在工程根
  Program.cs           入口，必须留在工程根
  Shell/               MainWindow.*、AppContext.cs
  Domains/<Area>/      一个目录一个功能域，索引见 DOMAINS.md
  Lexicon/             词汇表与 9 份语言文件
  Infrastructure/      叶子依赖（动效、文件、数学）
  Platform/            Win32 互操作与配置部署
  Assets/ Languages/ Menus/ Styles/ Themes/    资源，随输出复制
  NativeMethods.* app.manifest Package.appxmanifest     MSBuild 约定，留在根
src/mpvw-native/       C++/WinRT 组件
  MpvPlayer.* MpvPreviewer.*   运行时类
  Types/ Events/              值类型与事件参数类型
  pch.* *.vcxproj .def        MSBuild 约定，留在根
src/mpvw-native-projection/   把 winmd 投影成 .NET 程序集并打 nupkg
data/                  配置层；mpv 要求主配置在根
tests/                 两个测试套件（conf / native）
tools/                 检查器、生成器、打包脚本
docs/                  契约与事实；索引是 docs/README.md
```

两条搬文件时会咬人的规则：

- **搬 `.xaml` 是免费的，但它会自己传播。** 子目录里的 `.xaml` 会编译到同相对路径的 `.xbf`，
  页面按资源 URI 加载。改源码，输出跟着走；**绝不**手工搬输出里的 `.xbf`。
- **MIDL 的 `import` 相对工程目录解析，不是相对导入它的那个文件。** 所以
  `Types/MpvTrack.idl` 必须写 `"Types/TrackType.idl"`。生成的 `.g.h` / `.winmd` 按名字写进
  中间目录，所以源码分目录不影响生成路径。

---

## 工作循环

1. **先跑 `python tools\check-xaml.py`，再构建。**
   XAML 注释里不能出现 `--`（WMC9997），而没有任何编辑工具会阻止你写。这件事在本仓库
   消耗过五个完整构建周期；第五次时构建失败、部署却仍然复制了上一次的二进制并报成功。
   `check-xaml.py` 一秒内抓到。**如果部署在一次失败的构建之后打印健康的 `identical: N`，
   把部署当成陈旧，不当成通过。**
2. **构建** `.\build.ps1 -Configuration Release -Platform x64`（工程根）。
   先用 VS MSBuild 编 C++ 组件，再 `dotnet build` 编应用。**用全名参数**，
   `-Release x64` 会静默退回 Debug。验收：退出码 0、无 error 行，且产物在
   `src/mpvw-app/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/mpvw.dll`。
   程序集名是 `mpvw`，不是 `mpv-winui`。
3. **跑检查器**——见下面「提交前」。
4. **行为验证**——启动应用、走 IPC、跑 UI 自动化。事实与方法见
   [`docs/runtime-facts.md`](docs/runtime-facts.md) §2–§4。
5. **交接**——报告构建或部署产物的路径，然后停下。

---

## 提交前

- **删掉调试插桩**：`grep -rn "\[DBG-"`。
- **只暂存仓库文件**：绝不 `bin/`、`obj/`、`dist/`、`libs/`。
- **BOM 与换行要对着索引检查，不是对着相邻文件检查。** 两者在这里都不统一，且都不会
  好好显示在 diff 里：丢掉的 BOM 会把第 1 行渲染成 `-﻿using`，而 `git diff --stat` 报成
  一个字符的改动；CRLF→LF 翻转在下次检出前完全看不出来。`edit` 类工具会丢 BOM，
  有些 Python 脚本会加上一个。两个方向都咬过这个仓库。信 `git diff --check` 加上
  "`--stat` 里没有整文件重写"。
- **检查器**：`build.ps1` 跑 `check-xaml`、`check-script-opts`、`check-winrt-events`；
  CI 另跑 `check-localization`、`check-settings-drift`、`gen-schema-options.py --check`。
  改 XAML 跑 `check-xaml` + `check-resources` + `check-scroll-maxwidth`；
  改设置跑 `check-settings-drift` + `check-conf-lines` + `check-mpvconf-precedence`；
  改分区跑 `check-sections` + `check-empty-sections` + `check-section-icons` + `check-localization`；
  改文档跑 `check-doc-freshness` + `check-doc-references` + `check-doc-tooling`。
  完整清单与「哪些没有自动化」见 [`docs/verification.md`](docs/verification.md)。

### 加检查器的三条规则

1. **先看它红。** 注入**真实**违规，不是合成违规——本仓库两次都是靠重新引入真正的那行
   订阅才复现的。没见过红的检查器，它的盲区是未知的。
2. **拒绝不可信的输入。** "解析到 0 项"必须是错误而不是健康，否则停止匹配的正则会
   永远报成功。
3. **问代码读什么，不要问注释说它读什么。**

> 反面教材：`check-ui-tooltips.py` 曾经打印 38 条发现、永远退出 0、且行号因为先删注释
> 再数换行而整体错位（`MainWindow.xaml` 522 行被删成 269 行，报第 55 行、实际在 133 行），
> 其中大量条目指向的控件其实在代码里已经命名。三条都违反。

---

## 改 X 之前读 Y

| 要改的东西 | 先读 |
|---|---|
| 跨目录搬代码、加文件到某一层 | [`docs/architecture.md`](docs/architecture.md)（四层、8 个域、实测依赖方向、每层禁止什么） |
| `Domains/Settings/**` 下任何选项行 | [`docs/settings-contract.md`](docs/settings-contract.md)（命名、分组、例外登记） |
| `MpvSettings.cs` 的 `ToCommand` / `MpvOptionNames` | [`docs/settings-contract.md`](docs/settings-contract.md) → [`docs/mpv-conf.md`](docs/mpv-conf.md) |
| `ManagedMpvConfig.cs` / `MpvConfOverrides.cs` | [`docs/mpv-conf.md`](docs/mpv-conf.md)（所有权按行判定；配置目录只有一个派生点） |
| `ConfigDeployer.cs` / `data/deploy-config.ps1` | [`docs/deploy-layout.md`](docs/deploy-layout.md)（两份手工同步的清单） |
| `Lexicon/**` / `Languages/*.json` / `Menus/menus.json` | [`docs/localization.md`](docs/localization.md) |
| `Infrastructure/Motion/**` 或任何动画 | [`docs/motion.md`](docs/motion.md)（时长/曲线/通道只有一种正确写法） |
| 想知道"为什么实际行为是这样" | [`docs/runtime-facts.md`](docs/runtime-facts.md)（管道、路径、DPI、截图、崩溃定位） |
| 提交前 | [`docs/verification.md`](docs/verification.md)（哪些必须跑、哪些没有测试） |
| 想"上游有这个我们为什么没有" | [`docs/upstream-gap.md`](docs/upstream-gap.md)（许可边界与逐条处置） |

`docs/README.md` 是索引，并规定了每份文档的类别与数字口径。**文档与代码冲突时以代码为准**，
并把冲突记进对应文档。

---

## 设置架构（改 `Domains/Settings/` 必读）

`docs/settings-contract.md` 是 `Domains/Settings/` 之下一切事物的**权威**。先读它。
以下是定位用的要点：

- `SettingsPage.Options.cs`：树构建器 `BuildSettings` 带四张表——`sectionMap`（每一行的分区，
  唯一来源）、`sectionOrder`、`optionOrder`，另加 `categoryOrder`。**没有 `sectionMap` 条目的行
  会让 `BuildSettings` 在打开时抛异常**（故意的，失败要响）。
- **树有两半。** 手写行在 `Options.*.cs` 分部类里；schema 生成行在
  `SettingsPage.Options.Gen.cs`（`tools/gen-schema-options.py`）。改生成器或
  `MpvConfOptions/mpv-options.json`，**绝不**直接改 `.Gen.` 文件——下次生成会覆盖。
  任何枚举选项、分区或 mpv 名字的东西都必须读**两半**；曾有六个检查器只读一半，
  它们加起来报了几万条幻影发现。
- `Controls/Option*.xaml/cs`：每个 `OptionType` 一个渲染器。`OptionType` →
  `OptionTemplateSelector` 的 switch → `OptionListControl` 的 `DataTemplate` 是 1:1:1，
  加一个类型意味着三处都要动。
- 折叠分区写在**三个**必须同步的地方：行标志 `Option.AdvancedSection`、页面的
  `FoldedSections` 集合、`OptionListControl.CollapsedSectionIds`。最后一个是**粘性**依赖属性，
  所以每个给 `OptionList` 赋值的分支都必须清掉它。
- 依赖闸门：`RefreshWarningsAndEnabled()` 求值 Warning / IsEnabled / IsVisible 规则。

---

## 跨语言契约（有检查器盯着）

本仓库有三个缺陷是同一个形状：**两样东西约定了契约，没有东西比较它们，断裂是静默的。**
每一个现在都有检查器。

| 契约 | 检查器 |
|---|---|
| C# 事件订阅 ↔ C++ `m_*Event` 抛出 | `check-winrt-events.py` |
| 本地 Lua 脚本 ↔ 固定版 mpv 发给它的 `script-opts` | `check-script-opts.py` |
| `AppLang` 属性 ↔ 九份语言文件 | `check-localization.py` |
| `AppSettings` ↔ `ToCommand` ↔ `MpvConfOptionNames` | `check-settings-drift.py`、`check-conf-lines.py` |
| 文档引用的路径 ↔ 干净检出里真实存在 | `check-doc-tooling.py` |

咬得最狠的三个：

- **`PlaybackFailed` / `VoConfigured`。** `c49b461` 改了原生侧的抛出名，IDL 保留了旧声明，
  于是两个 C# 订阅都能编译、永不触发。`MediaPlayer_MediaFailed` 从未执行，`Error` 视觉状态不可达。
- **`populate_menu_data`。** `mpv-2.dll` 为它内置的 `select.lua` 硬编码
  `script-opt=select-populate_menu_data=no`；项目本地化的那份只声明了五个选项中的两个，
  于是每次启动都记一条 "unknown key … ignoring" 并丢掉该值。
- **`MpvSpaceXS`。** 一个与键差一个字符的 `{StaticResource}` 名在 `InitializeComponent()` 里抛异常，
  而 `Frame.Navigate` 把它吞了——症状是空窗口，无对话框、无日志行。

---

## 无障碍（改 XAML 必守）

图标按钮必须同时有可访问名称与工具提示。UIA **不会**把子 `TextBlock` 的文字折进父控件的名字，
所以"看得见文字"不等于"有名字"。

本仓库的写法（照其中一种，不要发明第三种）：

- 控件在页面里、文案随语言变 → 在对应的 `Apply*Strings` 方法里
  `AutomationProperties.SetName(...)` + `ToolTipService.SetToolTip(...)`，与设置可见文字同一次。
- 名字来自子 `TextBlock` → `AutomationProperties.LabeledBy="{Binding ElementName=那个TextBlock}"`。
- `DataTemplate` 里的控件、只能按名字取 → `FindDescendant` 之后 `SetName`／`SetToolTip`
  （见 `ApplyCardOverlayStrings`）。

`check-ui-tooltips.py` 会核对这三条路径；它有失败路径，报错即可视为回归。

---

## 许可证

- 应用代码：LGPL-2.1。本项目自写的配置层文件：LGPL-2.1-or-later。
- `dyn_menu.lua` 是 GPL-2.0-only 源码，带一处有记录的本地补丁；
  `dialog.lua` 已移除（不再有菜单引用它）——见 `data/scripts/LOCAL-PATCHES.md`。
- 加第三方组件时更新 `data/licenses/THIRD_PARTY_NOTICES.md`。
- `main` **刻意不设上游跟踪分支**：理由与后果见 [`docs/upstream-gap.md`](docs/upstream-gap.md) §1.1。
  例行 `git pull` 会把 GPLv3 期的提交拉进来，本仓库的 LGPL-2.1 声明随即失效。
