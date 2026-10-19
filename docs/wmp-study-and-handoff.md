# 官方媒体播放器源码调研 + 动效体系交接

日期：2026-10-11 · 目的：回答"能否参考官方 Media Player"，并给新项目一份可直接粘贴的启动提示词。

---

## 一、源码问题的结论

**两个都是闭源。没有官方源码可拿。**

| 产品 | 状态 | 依据 |
|---|---|---|
| Windows Media Player Legacy（`wmplayer.exe`，WMP 12） | 闭源专有 | 无公开仓库 |
| 新版 Media Player（`Microsoft.ZuneMusic`） | 闭源专有 | 无公开仓库 |

⚠️ 网上有站点声称"微软已开放 WMP 11 源码供下载"，**不要采信**。真实存在的是
**Windows Media Format SDK / Windows Media Player SDK** —— 那是调用 API 和文档，
不是播放器本体源码。

### 新版 Media Player 到底是什么技术栈（本机实测）

2026-10-11 在 `C:\Program Files\WindowsApps\Microsoft.ZuneMusic_11.2609.11.0_x64__8wekyb3d8bbwe`
实读 `AppxManifest.xml`：

```
TargetDeviceFamily : Windows.Universal   Min=10.0.19042.0
Dependencies       : Microsoft.UI.Xaml.2.8            ← WinUI 2.8
                     Microsoft.NET.Native.Framework.2.2 ← UWP .NET Native
                     Microsoft.NET.Native.Runtime.2.2
                     Microsoft.VCLibs.140.00
Executable         : Microsoft.Media.Player.exe (10 KB 引导壳)
EntryPoint         : Microsoft.Entertainment.Music.App
```

递归搜索包内 `Microsoft.WinUI.dll` / `WindowsAppRuntime.dll` /
`Microsoft.WindowsAppRuntime.Bootstrap.dll` / `Microsoft.ui.xaml.dll` —— **全部不存在**，
清单里也没有声明任何 Windows App SDK 依赖。

**结论：它是 UWP（WinUI 2.8 + .NET Native），经 Desktop Bridge 打包，不是 WinUI 3。**
社区把它列进 WinUI 3 应用清单，指的是"长得像 WinUI 3"，不是技术栈。

---

## 二、为什么它拖窗口、开应用、播动画都比我们顺

这是**框架架构差异**，不是我们没调好。官方在 microsoft/microsoft-ui-xaml#5148
里由 Sergio Pedri 给出了明确解释：

- **UWP**：窗口内容整体交给 **DWM** 合成。窗口边框和内容在同一个合成器里，
  拖动/缩放天然同步，不存在"内容落后于窗口框"这一帧。
- **WinUI 3**：使用 **lifted compositor**。应用自己往宿主 HWND 里投放一条 swapchain，
  DWM 只是把这条 swapchain 拿去合成。HWND 的边框按 Windows 的方式 resize，
  XAML 内容按自己的节奏 resize，两者之间的缝会露出背景（黑或云母）——
  表现为拖动/缩放时的撕裂与可感知延迟。

同一个 issue 里 Microsoft 也承认了这一点，并在 2026 年通过 Windows App SDK
推送了更平滑的缩放改进（另有一处 `TitleBar.IconSource` 破坏 resize 的 bug
在 Windows App SDK 1.8.5 修复）。

**所以能学什么、不能学什么：**

| 维度 | 能否追上 | 说明 |
|---|---|---|
| 拖动/缩放窗口的物理手感 | ❌ 追不平 | 架构决定，应用层只能缓解 |
| 应用冷启动 | ✅ 基本已追平 | 我们是 Native AOT，它也是 AOT（.NET Native） |
| 动画流畅度 | ✅ 可以显著改善 | 关键是把动画从 UI 线程搬到合成线程 |
| 界面排版与美术风格 | ✅ 完全可以 | Fluent 规范是公开文档，不是它的私有资产 |
| 选项切换的逻辑与手感 | ✅ 完全可以 | 用 WinUI 3 官方控件 + 官方动效曲线 |

**可缓解拖动延迟的具体方向**：不要走 `ExtendsContentIntoTitleBar` 自绘拖拽区
（本项目有个「隐藏原生标题栏」选项，需要实测开关前后的帧率与手感差异，
这是第一个该做的实验）。

---

## 三、可参考的项目（都是有源码的）

### 官方

| 项目 | 地址 | 价值 |
|---|---|---|
| WinUI 3 Gallery | `github.com/microsoft/WinUI-Gallery` | **第一参考**。控件、样式、设计指南、动效的官方交互式样例，含 MediaPlayerElement 页 |
| WindowsAppSDK-Samples | `github.com/microsoft/WindowsAppSDK-Samples` | 含 `Samples/SceneGraph`（原 WindowsCompositionSamples 迁移至此）——Composition / Input 底层动画与交互的官方样例 |
| microsoft-ui-xaml | `github.com/microsoft/microsoft-ui-xaml` | WinUI 框架本体源码 |
| Windows Community Toolkit | `github.com/CommunityToolkit/Windows` | `CommunityToolkit.WinUI.Animations`、`Lottie`、`Segmented`、`SettingsControls` 等 |
| Windows-universal-samples | `github.com/microsoft/Windows-universal-samples` | `MediaPlayback` 是 UWP 媒体 UI 的官方写法——**形态上最接近官方 Media Player** |

### 社区真实应用（看工程结构与动效落地）

| 项目 | 地址 | 价值 |
|---|---|---|
| Files | `github.com/files-community/Files` | 最成熟的 WinUI 3 桌面应用 |
| PowerToys | `github.com/microsoft/PowerToys` | 设置页与 Launcher 用 WinUI 3 |
| Dev Home | `github.com/microsoft/devhome` | 官方团队自己写的 WinUI 3 应用 |
| WinUI 3 Apps List | `github.com/GID0317/WinUI-3-Apps-List` | 420+ WinUI 3 应用清单，用来找同类参考 |

⚠️ **不要反编译官方 Media Player 的 XBF/DLL 用于发布**。设计语言可以学
（Fluent 规范本来就是公开的），代码不能抄。

---

## 四、官方动效规范：可直接落地的数值

来源：`learn.microsoft.com/windows/apps/design/signature-experiences/motion`
与 `.../design/motion/timing-and-easing`

### 标准时长（WinUI 内置 theme resource，自定义动画也应复用）

| 资源名 | 值 |
|---|---|
| `ControlFasterAnimationDuration` | 83 ms |
| `ControlFastAnimationDuration` | 167 ms |
| `ControlNormalAnimationDuration` | 250 ms |

### 用途 → 缓动 → 时长

| 用途 | 缓动 | 曲线 | 时长 | 可动画属性 |
|---|---|---|---|---|
| 直接入口 | 快入 | `cubic-bezier(0, 0, 0, 1)` | 167 / 250 / 333 | 位置、缩放、旋转 |
| 现有元素位移 | 点到点 | `cubic-bezier(0.55, 0.55, 0, 1)` | 167 / 250 / 333 | 位置、缩放、旋转 |
| 直接退出 | 快出 | `cubic-bezier(0, 0, 0, 1)` | 167 | 位置、缩放、旋转（**必须配合淡出**） |
| 平缓退出 | 软出 | `cubic-bezier(1, 0, 1, 1)` | 167 | 位置、比例 |
| 最低限度 | 淡入 + 淡出 | 线性 | 83 | 不透明度 |
| 强势入口 | 弹性入（3 关键帧） | `(0.85,0,0,1)` → `(0.85,0,0.75,1)` → `(0.85,0,0,1)` | 167 → 167 → 333 | 位置、比例 |

### 落地方式

- **Composition 而不是 Storyboard。** XAML Storyboard 跑在 UI 线程，业务逻辑一忙就掉帧；
  `Microsoft.UI.Composition` 的动画跑在合成线程，不受 UI 线程影响。
  曲线用 `Compositor.CreateCubicBezierEasingFunction` 构造。
- 优先用**已有控件**（规范里明确写了"机智：尽可能利用现有控件"）：
  `PageTransitions`（页面切换）、`ConnectedAnimation`（层到层）、
  `AnimatedIcon` + Lottie（微交互）、`ItemsRepeater` + 隐式动画（列表进出）。
- **必须尊重减少动画设置**：`UISettings.AnimationsEnabled`；关闭时直接给终值。

### Fluent 五原则（评审动画时用）

已连接（元素状态间无缝过渡）· 一致（同一入口点的行为一致）· 响应式（明确反馈用户输入）·
令人愉快（短暂而有意义的惊喜）· 机智（优先复用现有控件，避免自造动画）

---

## 五、我们已经做了什么（给新项目的起点）

### 项目地址

```
源码  : C:\Users\z2637\Documents\DeepSeek\mpv
应用  : C:\Users\z2637\Documents\DeepSeek\mpv\mpv-winui\mpv-winui
部署  : C:\Users\z2637\Documents\DeepSeek\mpv-deploy   （用户自行双击测试）
Git   : HEAD b7fa7b8，无 git remote，约 60 个文件改动
```

### 技术栈

- **WinUI 3 / Windows App SDK 2.5.1**，`net10.0-windows10.0.26100.0`，最低 17763
- **libmpv**（C API）+ C++/WinRT 互操作层 `mpv_winrt`（独立 VS MSBuild 编译）
- C# 单项目 MSIX + 便携 zip；产物 `mpvw.exe` / `mpvw.dll`

### 已做的性能优化（很多正是官方实测的那批）

```xml
<PublishAot>true</PublishAot>              <!-- Native AOT -->
<PublishTrimmed>true</PublishTrimmed>
<SelfContained>true</SelfContained>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<IsAotCompatible>true</IsAotCompatible>
<EnabledXamlOptionalChanges>
  DefaultStyleOptimizations,OptimizeApplyStyles,
  IconNoGridOptimization,DeferContextFlyoutInit
</EnabledXamlOptionalChanges>
```
另外还有：剔除 WinUI 的 78 个无用本地化资源目录（.mui）、剔除 WindowsAppSDK
元包拖进来的 AI/ML 组件（曾带入 20.7 MB onnxruntime.dll + 17.8 MB DirectML.dll）。

### 已做的界面工作

- **设置项文案**：820 项 mpv 选项里，生成的 373 行**全部**有人工撰写的英文标题与中文说明
  （来源：`MpvConfOptions/labels.hand.json`）
- **设置项审计**：分两轮砍掉 179 项不适用选项（终端/其他平台/会锁死会话/按数值调参）
- **分区体系**：59 个分区、9 个分类，图标零冲突；含两个新分区
  「章节与时间线」「窗口与界面」
- **mpv.conf 应用内编辑器**：VS Code 深色配色、行号、语法着色、实时保存
- **校验脚本**：`tools/check-*.py` 十余个（XAML 格式、分区、占位符、本地化、
  图标冲突、mpv.conf 优先级、搜索索引……），改动前后都要跑

### 关键设计约束（新项目必须知道）

- `mpv.conf` 是唯一真源，用户在文件里手写的行**优先级最高**，
  界面写入靠 `# === mpv-winui managed options ===` 标记块（详见
  `docs/mpv-conf-precedence.md`）
- **不要启动应用做验收**。用户明确要求：只编译、只跑静态检查、把路径交给他自己双击测
- 验证靠可复现的度量（像素统计、DIP 实测值、文件哈希），不靠肉眼看截图缩略图

---

## 六、新项目要交付什么

### 1. 一套 Fluent 动效体系（不是零散动画）

按第四节的三档时长 + 四类曲线建一组可复用资源：时长令牌、缓动函数、
页面转场、卡片进出、列表项增删、状态切换（开关/选中/展开）。
全部走 Composition，并统一响应"减少动画"设置。

### 2. 一个空白主页

参考官方 Media Player 的信息架构（左侧导航 + 顶部标签页 + 内容网格 + 底部播放条），
但**只做骨架**：导航区、页头、内容容器、空状态，全部带进出动画与转场。
不接真实数据，目的是把动效体系跑通并可视化验证。

### 3. 设置界面的官方级排版 + 选项切换逻辑 + 交互动画

- 排版：分区卡片、行模板（标签 + 说明 + 控件三列）、间距令牌、单行截断
- 切换逻辑：布尔开关、枚举下拉、数值拖动、多选列表的状态机与即时生效反馈
- 交互动画：行展开/折叠、控件状态过渡、保存反馈、分区切换转场

### 4. 明确的验收指标

- 动画掉帧：用 Windows Performance Analyzer 的 CPU Usage + XAML Activity 抓帧
- 启动：进程启动 → 首帧 UI 的耗时（WPA 的 WinUI3 startup region）
- 拖动：需要先做「隐藏原生标题栏」开关的 A/B 实测，确定当前是否踩了自绘拖拽区的坑

---

## 七、待验证 / 有风险的点

1. **Windows App SDK 2.5.1 是否已含 2026 年那次"更平滑缩放"的平台改进** —— 需查
   2.5.x 的 release notes 确认，否则升级到更高版本可能直接改善手感。
2. **本项目窗口拖动延迟的真正来源未定位**：是 lifted compositor 的固有代价，
   还是"隐藏原生标题栏"自绘拖拽区引入的额外开销。这是新项目第一个该做的实验。
3. **Native AOT + WinUI 3 对 Composition 动画与 Lottie 的兼容性**需要实测
   （裁剪可能吃掉反射路径）。
4. 官方 Media Player 播放顺滑还包含它用系统解码栈的因素；我们走 libmpv，
   播放链路的延迟是另一条独立的线，不要和 UI 动效混在一起优化。

---

# 附：可直接粘贴到新项目的启动提示词

```
我要做一个 WinUI 3 的动效与界面骨架项目，参考微软官方 Media Player 的观感。

## 背景（我已经调查过，不用重复调查）

1. 官方 Media Player 是闭源的，且经我本机实测确认：它是 UWP + WinUI 2.8 + .NET Native
   （AppxManifest 依赖 Microsoft.UI.Xaml.2.8 / Microsoft.NET.Native.*，
   TargetDeviceFamily = Windows.Universal，未声明任何 Windows App SDK 依赖）。
   它拖窗口/开应用更顺，是架构差异，不是调优差距：
   UWP 的内容由 DWM 直接合成，WinUI 3 用 lifted compositor 自己投 swapchain，
   所以 WinUI 3 拖动/缩放时 HWND 边框与 XAML 内容会不同步（见
   microsoft/microsoft-ui-xaml#5148）。这一条我们追不平，不要试图追。
2. 但**界面排版、美术风格、动效曲线、选项切换手感**全部可以学，因为
   Fluent 规范是公开文档。

## 要交付的东西

A. 一套可复用的 Fluent 动效体系（不是零散动画）
B. 一个空白主页，只做骨架：左侧导航 + 顶部标签页 + 内容区网格 + 底部播放条，
   全部带进出动画与页面转场，不接真实数据
C. 设置界面的官方级排版 + 选项切换逻辑 + 交互动画

## 必须遵守的技术约束

- 动画一律用 Microsoft.UI.Composition（合成线程），不用 XAML Storyboard（UI 线程）
- 时长只用官方的三个值：83ms / 167ms / 250ms（ControlFaster/Fast/NormalAnimationDuration）
- 缓动曲线：
    入口  cubic-bezier(0, 0, 0, 1)
    位移  cubic-bezier(0.55, 0.55, 0, 1)
    退出  cubic-bezier(1, 0, 1, 1)（退出必须配合淡出）
    淡入淡出 线性 83ms
  用 Compositor.CreateCubicBezierEasingFunction 构造
- 必须响应系统"减少动画"设置（UISettings.AnimationsEnabled），关闭时直接给终值
- 优先复用现有控件：PageTransitions / ConnectedAnimation / AnimatedIcon /
  ItemsRepeater 隐式动画。遵循规范里"尽可能利用现有控件"这一条，不要自造动画
- 目标：Windows App SDK 2.5.1 起，net10.0-windows10.0.26100.0，最低 17763
- 构建用 Native AOT（PublishAot / PublishTrimmed / SelfContained）
- 不要启动应用做验收测试。只编译 + 跑校验脚本，然后把可执行文件路径给我，我自己测

## 可参考的项目（都有源码）

- github.com/microsoft/WinUI-Gallery            第一参考，控件与动效的官方样例
- github.com/microsoft/WindowsAppSDK-Samples    含 Samples/SceneGraph，Composition 与 Input 的官方样例
- github.com/CommunityToolkit/Windows           CommunityToolkit.WinUI.Animations / Lottie
- github.com/files-community/Files              最成熟的 WinUI 3 桌面应用
- github.com/microsoft/PowerToys                设置页的 WinUI 3 写法

参考设计语言可以，不要反编译官方 Media Player 的资源用于发布。

## 我已有的代码库（可以借鉴的做法）

源码   C:\Users\z2637\Documents\DeepSeek\mpv
应用   C:\Users\z2637\Documents\DeepSeek\mpv-winui\mpv-winui
部署   C:\Users\z2637\Documents\DeepSeek\mpv-deploy
技术栈 WinUI 3 + Windows App SDK 2.5.1 + libmpv(C API) + C++/WinRT 互操作层

它已经完成的界面工作：
- 设置项文案：820 项 mpv 选项，生成的 373 行全部有人工撰写的英文标题与中文说明
- 设置项审计：两轮砍掉 179 项不适用选项
- 分区体系：59 个分区 / 9 个分类，图标零冲突
- 十余个静态校验脚本在 tools/check-*.py
- 性能优化已做：Native AOT、裁剪、自包含、XAML 可选优化、剔除无用本地化资源、剔除 AI/ML 组件

## 第一步请先做

先定位一个具体问题再动手：该窗口拖动延迟到底来自 lifted compositor 的固有代价，
还是来自自绘拖拽区（它有一个「隐藏原生标题栏」选项会走
ExtendsContentIntoTitleBar 自绘）。做一个开关前后的 A/B 实测，
用 Windows Performance Analyzer 的 CPU Usage + XAML Activity 抓帧对比，
拿到数据后再决定动效体系怎么设计。
```
