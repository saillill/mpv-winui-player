# mpvw 的 Skin / SCP 调研报告

**日期**：2026-10-21（本地证据取自 HEAD `7b42fd5`；外部方案核对于 2026-10-22）
**范围**：只调研，不改代码，不启动 `mpvw.exe`。
**方法**：本地结论全部由 `file:line` 取证；外部方案全部经 `api.github.com` / `mpv.io` / `learn.microsoft.com` 实取，未能取到的明确写「未核实」。

---

## 0. 一句话结论

本项目语境下的 **Skin = L1 应用级主题包 + L2 播放器控制栏皮肤**，两者是同一份「外观」设置的两个层次（包 / 形态），不是两个并列功能；**L3（uosc / oscc 这类 mpv 进程内 OSC）明确不引入**——不是因为不好，而是因为它需要一个本项目没有的东西：一个独立的 mpv 窗口。

另外，调研过程中发现一处**静态证据支持的结构性问题**（§2.4）：两套备用皮肤是**整份复制**的 `ControlTemplate`，里面没有 `ContentPresenter`，而 109 个代码字段绑的是 `PlayerControl.xaml` 内容树里的元素。按静态阅读，切到 Centre/Compact 后画面会出现另一条外观正确但**没有被接线**的控制栏。这条必须由你在真机上点一下确认，本报告不替你下结论。

---

## 1. 术语判定：Skin 在本项目指哪几层

### 1.1 三层候选的实况

| 层 | 是什么 | 本项目现状 | 归属工具 |
|---|---|---|---|
| **L1 应用级主题** | `App.xaml` 的 `Application.Resources` + `ThemeDictionaries` + 品牌色阶 | **部分存在**：有 Light/Dark 两套主题字典（`Styles/Styles.xaml:9`、`:103`），但**没有**任何换肤/主题包机制 | WinUI `ResourceDictionary` |
| **L2 播放器控制栏皮肤** | `AppSettings.PlayerStyle` 的 Standard / Centre / Compact | **已存在**，3 个值，有设置入口、有实时生效路径 | XAML `Style` + `ControlTemplate` |
| **L3 mpv 进程内 OSC** | uosc / oscc / mpv-osc-modern 这类画在 mpv 画面上的 Lua UI | **明确不存在且被硬关掉**：`MpvPlayer.cpp:254` `SetOption("osc","no")`、`mpv-winui-lazy/mpv.conf:198` `osc = no`；15 个 Lua 里没有任何 OSC | mpv Lua |

### 1.2 建议

**把 Skin 定义为 L1 + L2 的合并语义，并把「SCP」定义为 L1 的落地单位。**

理由，逐条对应代码：

1. **L1 与 L2 在数据上已经是同一件事。** 用户的「外观」入口只有一处：设置 → Program → **Player appearance**（`SettingsPage.Options.Program.cs:34-70` 的 `PlayerStyle` 行），同一 Category 下紧邻的是 `ThemeType`（`:75`）、`BackdropType`（`:97`）、`UiFont`（`:119`）。四行都是「这台播放器长什么样」，`95ee55c` 的提交信息还专门解释了为什么把 `PlayerStyle` 从 Interface 分出来时**没有**并进窗口外观——它属于播放器，但仍是外观。把它们叫做两个互不相干的「Skin」概念，用户体验上站不住。
2. **L2 已经是一个完整的「形态」轴，缺的只是「包」轴。** 现有 3 套皮肤改的是**布局**（控件在哪、有没有音量滑块），不改**颜色**：`CenterPlayerControlStyle.xaml` 与 `CompactPlayerControlStyle.xaml` 的取色全部走共享 token——`MpvPlayControlBackground`、`MpvTransportPlayRingBrush`、`MpvTransport*`、`SystemAccentColor2Brush`（实测每个字典引用 13 / 10 个 key，其中 10 / 8 个定义在 `Styles/Styles.xaml`）。也就是说今天改颜色要改 `Styles.xaml`，改布局要改皮肤文件，两条路互不相干——这正是需要 L1 的原因。
3. **L3 缺的是前置条件，不是意愿。** 见 §4.1：所有 mpv OSC 方案都假定「鼠标事件到达 mpv 窗口 + OSD 画在整个窗口上」，而本项目是 `d3d11-output-mode=composition`（`MpvPlayer.cpp:270`）的进程内嵌入，OSD 只会画进 `SwapChainPanel` 的合成层（`MpvPlayerPage.xaml:1002`），且鼠标是**转发**进去的（`MpvPlayerPage_Mouse.cs:20-39`，默认开：`AppSettings.cs:296-299`）。能跑，但要在 WinUI 控制栏之外再养一套同功能的、只看得到第二套 UI 的东西，收益为负。
4. **「SCP」在本仓库零命中。** 全仓 `grep \bSCP\b` 只命中 `docs/project-overview-20261021.md:241` 和 `:257`，都是「待定义」的指代。所以它可以被自由定义，而**定义成什么要能指导落地**。

### 1.3 三条术语的最终建议

> **Skin**：一套外观 = **L1 主题包**（颜色阶、字体、圆角、密度 token）+ **L2 控制栏皮肤**（Standard / Centre / Compact 三选一）。用户侧一个概念，实现上两个正交轴。
>
> **SCP（Skin Capability Package）**：L1 主题包的可分发单位——`manifest + token 字典 + 可选资源（字体/图标）`。一份 SCP 不携带任何可执行逻辑，只携带键值。
>
> **OSC**：本项目一律指「mpv 自己画的 Lua 控制界面」，是**外部方案**，不是本项目的皮肤层。

命名提醒：`SCP` 与 Windows 自带的 `scp`（OpenSSH 安全复制）、SCP 基金会、以及本期报告标题里的「SCP」三重撞名。若这份东西要进用户可见文档，建议改叫 **`SkinPack`** 或 **`主题包`**；内部文档沿用 SCP 无妨。

---

## 2. 本地现状（含 `file:line`）

### 2.1 L1：应用级主题的真实结构

`App.xaml` 是唯一的应用级资源入口，合并顺序固定：

```
App.xaml:6-13   Application.Resources → MergedDictionaries
App.xaml:9      XamlControlsResources                      ← 必须第一（官方要求，见 §3.4）
App.xaml:10     ms-appx:///Styles/Styles.xaml              ← 119 个 x:Key
App.xaml:11     ms-appx:///Modules/Player/CenterPlayerControlStyle.xaml
App.xaml:12     ms-appx:///Modules/Player/CompactPlayerControlStyle.xaml
```

设计师在 `App.xaml:15-44` 留了 30 行注释解释品牌色阶为什么放在这里，其中的两条结论可以直接复用为约束：

- 覆盖**七个 `Color`**（`:45-51`）能让整套 accent brush 跟着走；
- 覆盖 **brush**（`AccentFillColorDefaultBrush` 之类）**无效**——无论放在应用字典还是合并到 `XamlControlsResources` 之后的字典，都会输给 WinUI 自己的定义。所以「换品牌色 = 换这 7 个 Color key」，没有第二条路。

这条经验与官方文档的例外条款正好对上：`SystemAccentColor*` 是**主题无关**资源，因此它只能放在应用字典的**主题字典之外**（`App.xaml:45-51` 就在 `MergedDictionaries` 之后、主题字典之外）。一个皮肤包如果要换品牌色，就必须走这条通道——见 §3.5。

`Styles/Styles.xaml` 的实测结构：

| 项 | 值 |
|---|---|
| 体积 / 行数 | 104 088 B / 1 622 行 |
| `x:Key` 总数 | **119**（其中 105 个 `Mpv*` 前缀；去重后 103） |
| 主题字典 | 只有 `Light`（`:9`）与 `Dark`（`:103`），**没有 `HighContrast`** |
| 跨主题重复的 key | 16 个（`MpvPlayControlBackground`、`MpvShell*`、`NavContent*`、`SystemAccentColor2Brush` 等）——即「同一 key 在 Light/Dark 各定义一次」，这正是主题字典的正确用法 |
| key 类型分布 | `SolidColorBrush` 28、`Style` 11、`x:Double` 43、`Thickness` 24、`Color` 4、`CornerRadius` 2、`FontFamily` 1 |

缺 `HighContrast` 是一个真实缺口：官方指南要求 Light / Dark / HighContrast 三套都给，否则高对比度模式下自定义 brush 不跟随系统（§3.1 引用）。

`Themes/Generic.xaml` 只有 498 B / 13 行，内容是两条**隐式** Style：

```
Themes/Generic.xaml:6-8     <Style BasedOn="{StaticResource DefaultSliderStyle}"   TargetType="ycc:SliderExt" />
Themes/Generic.xaml:10-12   <Style BasedOn="{StaticResource DefaultCheckBoxStyle}" TargetType="ycc:CheckBoxExt" />
```

它的存在理由在 `SliderExt.cs:14` / `CheckBoxExt.cs:12`：两个控件都设了自己的 `DefaultStyleKey`，框架按约定到 `Themes/Generic.xaml` 找默认样式。**结论：`Themes/` 这个目录名在本项目里已经被框架语义占用（默认样式字典），不适合再拿来放皮肤。** 见 §6.1。

### 2.2 L2：三套控制栏皮肤的完整链路

```
设置行        SettingsPage.Options.Program.cs:34-70
              :43-47  AllowCustom = false + 注释「Three real skins, no Custom entry」
              :51-53  Choices = Default / Center / Compact
              :64-66  Setter 里 NotifySettingChanged（立即生效，不等重启）

存储          AppSettings.cs:308-310  PlayerStyle_Default="Default" / _Center="Center" / _Compact="Compact"
              AppSettings.cs:312-315  PlayerStyle 属性，默认 Default

下发          PlayerControl.xaml.cs:486-504  OnAppSettingChanged → :502 DispatcherQueue.TryEnqueue(ApplyPlayerStyle)
              PlayerControl.xaml.cs:903        AppContext.SettingChanged += OnAppSettingChanged
              PlayerControl.xaml.cs:1028       -=（订阅/退订成对）

套用          PlayerControl.xaml.cs:94-142   ApplyPlayerStyle()
              :110  Center  → Application.Current.Resources["CenterPlayerControlStyle"] as Style
              :122  Compact → Application.Current.Resources["CompactPlayerControlStyle"] as Style
              :130-137 default → Style = null（回到 UserControl 自带模板）

启动一次      MpvPlayerPage_PlayControl.cs:10-12 → :58 UpdatePlayControlStyle() => PlayerControl?.ApplyPlayerStyle()
PiP 窗口      PiPWindow.xaml.cs:207  PiPControls.ApplyPlayerStyle()（:203-206 注释说明它曾经一次都没被调用）
```

皮肤文件本体（实测）：

| 文件 | 体积 | `ControlTemplate` | `ContentPresenter` | `CommandBar` | `x:Name` | 内部 `Click=` |
|---|---|---|---|---|---|---|
| `PlayerControl.xaml`（默认形态） | 59 882 B | **0**（是 UserControl 内容，不是模板） | 0 | 7 | **105** | 5 |
| `CenterPlayerControlStyle.xaml` | 54 384 B | 1 | **0** | **0** | 90 | 0 |
| `CompactPlayerControlStyle.xaml` | 36 619 B | 1 | **0** | 0（1 处注释） | 74 | 0 |

两份皮肤都只声明一个带 `Template` setter 的 Style：

```
CenterPlayerControlStyle.xaml:10-15     <Style x:Key="CenterPlayerControlStyle" TargetType="player:PlayerControl">
                                          <Setter Property="Template"><Setter.Value>
                                            <ControlTemplate TargetType="player:PlayerControl">
CompactPlayerControlStyle.xaml:10-15    同上，x:Key="CompactPlayerControlStyle"
```

也就是说：**「三套皮肤」的实现方式是「三份并列的界面描述」**——默认形态在 `PlayerControl.xaml` 的 UserControl 内容里，另外两套各自持有一份完整的 `ControlTemplate` 副本（含 `ProgressSlider`、三个 CommandBar 的替代物、`MoreFlyout`、6 组 VisualState）。这不是 skinning，是 fork。

### 2.3 没有的东西（逐项确认）

| 期望存在 | 实测 |
|---|---|
| 应用级主题包 / 皮肤切换机制 | **无**。全仓无 `Skin` 枚举、无 manifest、无皮肤目录；`ThemeType` 只是亮/暗/跟随系统三值（`AppSettings.cs:166-185`） |
| 运行时换 `RequestedTheme` | 只有**元素级**：`WindowStyleManager.cs:143-146` `_contentRoot.RequestedTheme = theme`；`Application.RequestedTheme` 从未被写（官方只允许启动前设置，见 §3.2） |
| `Themes/Generic.xaml` 被 App 合并 | **没有**。全仓 `grep Themes/Generic` 零命中，也没有 `x:Class`——它靠框架约定被默认样式查找使用 |
| uosc / oscc / mpv-osc-* | **无**。`mpv-winui-lazy/scripts/` 15 个 `.lua`（另有 2 个 `.md`）：`auto_sub_fonts_dir`、`console`、`coverart`、`dyn_menu`、`dynamic_menu`、`hdr_auto`、`metadata_osd`、`mpvw_hdr_override`、`recentmenu`、`save_global_props`、`seek_hold`、`select`、`stats_mediainfo`、`stats`、`vsr_auto` |
| 独立 mpv 窗口 / mpv.exe | **无**。发布包 `dist/mpv-winui-win-x64-Release.zip` 158 项，可执行文件只有 `mpvw.exe`、`RestartAgent.exe`、`mpv-winui-lazy/tools/MediaInfo.exe`——**不含 `mpv.exe`**。渲染走进程内 `libmpv`（`mpv-2.dll` v0.41.0-dev-ge8673660a） |
| 配置层的分发方式 | `mpv-winui.csproj:192` 把整个 `mpv-winui-lazy/**` 作为 Content 复制到输出；Lua 随包走，没有单独的「插件市场」概念 |

### 2.4 结构性问题：皮肤模板与代码字段不在同一棵树（静态证据，需真机确认）

**证据链：**

1. `PlayerControl` 是 `UserControl`（`PlayerControl.xaml.cs:48`），`PlayerControl.xaml:45` 的 `<Grid x:Name="RootGrid">` 是它的 **Content**，不是 `ControlTemplate`（该文件 `ControlTemplate` 计数为 0）。
2. `PlayerControl` 的 109 个字段赋值**全部指向这棵内容树**，由 `InitializeComponent()` 生成的 `Connect()` 一次性完成（`obj/.../PlayerControl.g.cs` 中 `this.ProgressSlider = …`、`this.LeftCommandBar = …` 等，共 109 处 `this.X = global::…`）。
3. 事件接线全部打在**这些内容元素**上：`PlayerControl.xaml.cs:901-928`（PlayPause / SkipBackward / VolumeMute / FullScreen / FullWindow / Shuffle / TrackSelection / Zoom / Previous / Next 的 `Click +=`）、`:954-960`（`ProgressSlider.ValueChanged/PointerPressed/PointerReleased/Tapped`）、`:968`（`VolumeSlider.GotFocus`）。
4. 两套备用皮肤用 `Style.Template` **替换**了 `UserControl` 的模板；而它们的模板里 **`ContentPresenter` 计数为 0**。
5. 模板内的同名元素（`ProgressSlider` 等，90 / 74 个）属于**模板命名空间**，只能通过 `GetTemplateChild` 取得；全仓 `OnApplyTemplate` 0 处、`GetTemplateChild` 1 处（还是注释），**没有任何代码去取它们**。

**按 WinUI 的模板语义推断**：切换后 `RootGrid` 这棵内容树不再被渲染（没有 `ContentPresenter` 承接），屏幕上换成模板自带的那一份；而所有 `Click`/`ValueChanged` 仍挂在不再显示的内容元素上。

**如果成立**，用户看到的将是：布局确实变了（`95ee55c` 的提交信息记录过「切换皮肤时 transport 移动了 8 DIP」，这与模板渲染成立一致），但**按钮点了没反应、进度条拖不动**。

**为什么这条不能靠读代码定案**：`UserControl` 接受 `Style.Template` 之后的实际渲染、`VisualStateManager.GoToState` 在模板态下的行为（现有 12 处 `GoToState` 调用，`PlayerControl.xaml.cs:2305-2473`），以及 `PointerPressed="ControlPanelGrid_PointerPressed"`（`PlayerControl.xaml:157`）这类**声明式**接线在内容树不显示时是否仍会触发，都有边界情况。**本条标记为「静态证据支持、行为未验证」**。顺带说明：皮肤模板里的同名元素（90 / 74 个）**不是**空的——它们是完整的控件声明，只是没有代码去接线；所以「布局变了对、按钮没反应」这个组合在静态上是自洽的。

#### 2.4.1 真机实测结果：上述推断被推翻（2026-10-22）

§2.4 的静态推断**已被实测否证**。用本轮建好的 MCP 通道跑了两次，命令与输出都在
`tools/mvpw-mcp/`：

**第一：三套皮肤下，播放按钮都能真的切换暂停。**

`python tools/mvpw-mcp/verify_skins.py`（加载一个本地片段 → 记录 `pause` →
调用真实 `PlayPauseButton` → 再读 `pause`）：

```
Standard  pause False -> True   toggled=True
Centre    pause False -> True   toggled=True
Compact   pause False -> True   toggled=True
```

**第二：皮肤确实被应用了，不是「设置不生效」的假通过。**

`python tools/mvpw-mcp/verify_skin_geometry.py`（逐个皮肤读取五个具名控件的
边界矩形）：

| 控件 | Standard | Centre | Compact |
|---|---|---|---|
| `PlayPauseButton` x | 1655 | 1743 | 1831 |
| `ProgressSlider` 左/右 | 381 / 3027 | 469 / 3115 | 557 / 3203 |
| `VolumeSlider` 左 | 429 | 517 | 605 |

Centre 与 Compact 相对 Standard 在**全部五个**控件上都不同 → 模板生效
（结论 A），而不是「设置无效、一切照旧」（结论 B）。

**为什么静态推断错了**：`UserControl` 的 `Template` 被替换后，它原有的内容树仍然
被模板承载；代码后置持有的是 `InitializeComponent()` 时拿到的**活元素引用**，
元素位置变了、引用没断。所以「模板里没有 `ContentPresenter`」不等于「内容树不再
渲染」，也就不会出现「布局变、按钮死」。

**这条保留在报告里而不是删掉**，因为按同样的静态推理重新推一遍的人还会得出同样
的错误结论。§2.4 的三条观察本身都是真的，错的只是从它们到「按钮失效」的那一步。

#### 2.4.2 仍然成立的部分

三份并列的界面描述（一份内容树 + 两份整份 `ControlTemplate` 副本，合计约 90 KB
XAML）在维护上仍然是负担：加一个控件要改三处，`x:Name` 重复 105 / 90 / 74 次。
这属于**结构成本**问题，不是功能缺陷。收敛成「一份控件 + 可替换 token」仍然值得做，
但优先级低于本轮结论所暗示的程度——它不会修 bug，只会减少未来的重复劳动。

---

## 3. WinUI 3 官方路径

### 3.1 ResourceDictionary / ThemeDictionaries 的正确写法

官方硬规则（[XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-theme-resources)）：

1. 自定义主题资源要**同时**给 `Light`、`Dark`、`HighContrast`；用 `Default` 作 key 不推荐。
2. `{ThemeResource}` 只用在 **Style / Setter / ControlTemplate / 属性 setter / 动画**里。
3. **主题字典内部一律用 `{StaticResource}`**。唯一例外：引用主题无关资源（`SystemAccentColor*`、`SystemColor*`）。
4. 违反第 3 条的具体后果有官方描述：brush 是**共享**对象，两棵 `RequestedTheme` 不同的子树引用同一 brush 时，框架重算其中一棵的 `ThemeResource` 会把 brush 的颜色「污染」到另一棵，症状是「打开一个浅色 Flyout，深色页面的某些部分跟着变浅」。

本项目里已经有人踩过第 3 条的同族问题并写成注释：`Styles/Styles.xaml:18` 在 Light 字典里用 `{StaticResource SystemAccentColorDark2}` 而不是 `ThemeResource`——这是**正确**写法（引用的恰是主题无关的 accent 资源，命中官方例外）。

照抄版（皮肤包 token 字典的最小骨架）：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
      <Color x:Key="SkinSurfaceColor">#F7F1E7</Color>
      <SolidColorBrush x:Key="SkinSurfaceBrush" Color="{StaticResource SkinSurfaceColor}" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
      <Color x:Key="SkinSurfaceColor">#17171A</Color>
      <SolidColorBrush x:Key="SkinSurfaceBrush" Color="{StaticResource SkinSurfaceColor}" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="HighContrast">
      <SolidColorBrush x:Key="SkinSurfaceBrush" Color="{ThemeResource SystemColorWindowColor}" />
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>

  <x:Double x:Key="SkinControlBarHeight">72</x:Double>
  <CornerRadius x:Key="SkinControlBarCornerRadius">12</CornerRadius>

</ResourceDictionary>
```

要点：字典内部 `StaticResource`；只有 HighContrast 那一支用 `ThemeResource` 引 `SystemColor*`；尺寸/圆角这类主题无关 token 放在主题字典**之外**。

### 3.2 运行时换肤的三种做法与各自代价

| 做法 | 官方依据 | 能做到什么 | 代价 |
|---|---|---|---|
| **A. 替换 `MergedDictionaries` 里的某一项** | 文档**没有**背书「运行期改字典会刷新已解析的 XAML」，反而明确说运行期 `Add/Insert` 对 XAML 资源引用**无效**（[ResourceDictionary](https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-resource-dictionary)） | 社区通行做法：换掉合并项后 `{ThemeResource}` 会重新求值 | ①无官方保证，必须实测；②只刷新**用 `ThemeResource` 引用**的地方，`StaticResource` 一律不动；③已赋值过 `Style` 的控件（`PlayerControl.xaml.cs:110/122` 就是这种）不会自动重取，必须显式重跑 `ApplyPlayerStyle()` |
| **B. 改 `Application.RequestedTheme`** | 官方明确：**只能在应用启动前设置**，运行期设置抛 `NotSupportedException`（[Application.RequestedTheme](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.requestedtheme)） | 只能在 `App()` 构造里定基调 | 换肤要重启；且它只影响「明/暗」，不解决「橙/蓝/青」这类包级差异 |
| **C. 逐元素 `RequestedTheme`** | 官方明确支持运行期改单个元素并即时生效 | 本项目**正在用**：`WindowStyleManager.cs:143-146` 对窗口内容根设 `ElementTheme`；`PiPWindow.xaml.cs:147`、`PlayerControl.PanelAnimation.cs:108` 甚至把局部强制成 Dark | ①只能翻明/暗两套主题字典，翻不出第三个包；②必然触发 §3.1 第 4 条的 brush 共享污染；③强制 Dark 的局部（PiP、控制面板动画）不吃浅色皮肤 |

**给本项目的取舍**：L1 皮肤包走 **A**（因为它是「换包」，不是「换明暗」），明暗继续走 **C**（现状已正确），**B 不用**（会逼用户重启，与 `PlayerStyle` 的「立即生效」承诺不一致——见 `SettingsPage.Options.Program.cs:60-63` 的注释）。

A 的最小可照抄实现（关键点：**皮肤永远合并到最后**，并显式重跑所有「代码里赋值过的 Style」）：

```csharp
// 放在 App.OnLaunched 之后任何时候；索引固定，别用 Last()
private const int SkinDictionaryIndex = 4; // App.xaml 里皮肤字典的位置

public static void ApplySkin(string skinUri)
{
    var merged = Application.Current.Resources.MergedDictionaries;
    var skin = new ResourceDictionary { Source = new Uri(skinUri) };
    if (merged.Count > SkinDictionaryIndex)
    {
        merged[SkinDictionaryIndex] = skin;   // 替换，不是叠加
    }
    else
    {
        merged.Add(skin);
    }

    // 替换字典不会重跑「代码里赋过值」的 Style，必须显式重来一遍
    foreach (var window in AppContext.OpenWindows)     // 现有 WindowManager 已有等价能力
    {
        window.RefreshPlayerStyle();                   // 内部即 PlayerControl.ApplyPlayerStyle + ApplyControlBarStyle
    }
}
```

`RefreshPlayerStyle` 必须同时覆盖 `ApplyPlayerStyle()`（`PlayerControl.xaml.cs:94`）与 `ApplyControlBarStyle()`（`PlayerControl.ControlBar.cs:70`）——两者一个换模板、一个重排三个 `CommandBar` 的主命令，漏一个就是「换了一半」。

### 3.3 `{ThemeResource}` 与 `{StaticResource}` 在换肤时的差异

官方原文两句话就够：

> `{ThemeResource}` 在应用加载时求值，**并在此后每次主题变化时重新求值**；`{StaticResource}` 只在 XAML 首次加载时求值，**不会更新**——相当于启动时做了一次查找替换。

对本项目的直接含义（实测计数）：

| 文件 | `{ThemeResource}` | `{StaticResource}` |
|---|---|---|
| `Modules/Player/PlayerControl.xaml` | 7 | **75** |
| `Styles/Styles.xaml` | 53 | 68 |
| `CenterPlayerControlStyle.xaml` | 9 | — |
| `CompactPlayerControlStyle.xaml` | 7 | — |

`PlayerControl.xaml` 的 75 处 `StaticResource` 里，绝大多数是 `Style` 与度量（这没问题——换肤时这些通常不换），但只要其中有**颜色**，它在换肤时就不会跟。§6.2 的加载顺序设计要把这条算进去。

### 3.4 合并顺序（官方硬约束）

> `XamlControlsResources` 会覆盖大量默认资源键，因此必须**第一个**加入 `Application.Resources`，否则它会盖掉你自己的样式。

本项目遵守了（`App.xaml:9`）。皮肤字典必须加在最后一位，理由同源：资源查找是「先主字典、后合并字典；多个合并字典取**最后加入**的」。这样皮肤就能压过 `Styles.xaml` 的默认值，而不必改 `Styles.xaml` 一个字节。

### 3.5 已实例化控件不刷新的坑（本项目逐个对得上）

| # | 坑 | 官方/本地依据 | 本项目会不会中 |
|---|---|---|---|
| 1 | `StaticResource` 冻结 | 文档 | 会：`PlayerControl.xaml` 75 处 |
| 2 | 运行期 `Add/Insert` 到字典对已解析 XAML 无效 | 文档原话「运行期加入的资源与 XAML 资源引用无关……修改字典也不会让已取到的资源失效」 | 这是 A 方案必须实测的原因 |
| 3 | 共享 brush 被两棵不同主题子树污染 | 文档 troubleshooting 节 | 会：全局 Light/Dark + `PiPWindow.xaml.cs:147` 强制 Dark + `PlayerControl.PanelAnimation.cs:108` 强制 Dark 并存 |
| 4 | `Application.Current.Resources[..]` 索引器按**应用主题**解析，不看元素主题 | 本项目已踩过并写了兜底：`Modules/Common/View/ThemeResource.cs:14-24`（先查 `ThemeDictionaries[ActualTheme]`，再退回索引器） | 已缓解，但只覆盖了 brush 这一种类型 |
| 5 | 代码赋值的 `Style` 不会随字典重取 | `PlayerControl.xaml.cs:110/122` | 会：§3.2 的 `RefreshPlayerStyle` 就是为此 |
| 6 | `Popup` / `Flyout` 是独立视觉树根，继承**应用**主题而非元素主题 | 本项目四处弹层的 chrome 是手写的（`MpvPlayerPopupPresenterStyle` 等），`PlayerControl.ControlPanel.cs:277` 还是运行时 `new` 出来的 | 会：换肤后弹层可能保持旧色 |
| 7 | `x:Bind` 默认 `OneTime` | 框架默认 | 皮肤相关绑定必须显式 `Mode=OneWay` |
| 8 | 运行期写 `Application.Current.Resources["ContentControlThemeFontFamily"]` 是否即时生效 | `WindowStyleManager.cs:54-65` 的 `UpdateUiFont` 就是这么做的 | **未实测**。按文档第 2 条，写法本身可疑；建议下一轮用同一套 `RefreshPlayerStyle` 思路改写，或在真机上确认字体切换是否立刻生效 |

第 8 条值得单独提一句：`UpdateUiFont` 是**唯一**一个「运行期写应用字典并期待界面更新」的现存路径，它同时也是 `UiFont` 设置的实现。如果它其实不生效，那么「字体」这个设置项今天就是半残——而字体恰好是 §5 能力矩阵里「WinUI 层」的一行。

---

## 4. mpv 侧成熟方案（全部联网核实）

核实时间 2026-10-22，数据源 `api.github.com`（star / license / pushed_at / 最近提交），星数为当日快照。

### 4.1 先说这个项目为什么天然排斥 OSC

三条本地事实，任何外部 OSC 方案都必须先跨过：

1. **`osc` 被硬关了两遍**：`mpv-winrt/MpvPlayer.cpp:254` `SetOption("osc","no")`，`mpv-winui-lazy/mpv.conf:198` `osc = no`（注释写明「WinUI 自带控制条」）。两者都改才可能开。
2. **没有独立窗口**：`d3d11-output-mode=composition`（`MpvPlayer.cpp:270`），画面是 `SwapChainPanel` 里的一条交换链（`MpvPlayerPage.xaml:1002`）。mpv 的 OSD/OSC 会被合成进这块画面，**不会**画到 WinUI 控制栏所在的窗口层。所以 OSC 一定和 WinUI 控制栏**叠在一起打架**。
3. **鼠标是转发的、且节流**：`MpvPlayerPage_Mouse.cs:12` `MouseMoveIntervalMs = 50`，`:22` 受 `EnableMouseInput` 开关（默认 true，`AppSettings.cs:296-299`），`:66-79` 在窗口未激活或盘片导航独占时直接丢弃。OSC 的 hover / proximity 交互全靠这条链路，20 Hz 且可被关闭。

### 4.2 `tomasklaen/uosc`

| 项 | 核实值 |
|---|---|
| URL | https://github.com/tomasklaen/uosc |
| Star | **3 459**（fork 113，open issues 32） |
| 最近提交 | `074b0869` 2026-10-16「fix: sub-second time in timeline not rounding to zero (#1255)」；`pushed_at` 2026-10-16 |
| 许可证 | **LGPL-2.1**（仓库根 `LICENSE.LGPL`，GitHub 识别为 `lgpl-2.1`） |
| 依赖 | mpv ≥ 0.33；缩略图可选配 thumbfast；`osd-bar=no` 建议 |
| 是否适合进程内嵌入、无独立窗口 | **不适合（作为替代品）**。它是「近距感应式完整 UI」：时间轴、音量条、速度条、右键菜单、轨道选择菜单、字幕在线下载（需网络）、播放列表——即一整套 OSC，设计目标是**取代**窗口里的控制栏，不是补充 |
| 与现有 `PlayerControl` 冲突 | **直接冲突**（同一块画面底部两份时间轴、两份播放按钮）；且它的菜单键盘导航依赖 mpv 收到按键，而本项目的按键是 `WH_KEYBOARD` 钩子转发的（`MpvPlayerPage_InputForwarding.cs:27-33`），文本输入时会被 `IsTextInputFocused()` 拦住（`:185-211`） |
| 可用之处 | 它的 **`uosc.conf` 配置面**是很好的「L1 token 该有哪些」的参考清单 |

### 4.3 `oscc`

| 项 | 核实值 |
|---|---|
| URL | https://github.com/0dist/oscc（`longtermfree/oscc` 重定向到同一仓库） |
| Star | **71**（fork 1，open issues 3） |
| 最近提交 | `32d127e1` 2024-10-02「Update README.md」；`pushed_at` 2024-10-02 |
| 许可证 | **未声明**。GitHub API `license: null`；仓库根无 `LICENSE` / `LICENSE.md`（均 404）；README 无许可段。它自述「Based on the original osc」，是 mpv `osc.lua` 的衍生，而 `osc.lua` 本身**没有版权头**、mpv 的 `Copyright` 文件也未逐文件列出 `player/lua/`——**确切许可状态未核实，视为不可分发** |
| 依赖 | `oscc.lua` 进 `scripts/`、`oscc.ttf` 进 `fonts/`；`mpv.conf` 需 `osc=no` + `cursor-autohide` + `osd-bar=no`；缩略图兼容 thumbfast |
| 是否适合进程内嵌入、无独立窗口 | **不适合**。同 4.1：仍是整条 OSC；且它读 `cursor-autohide` 做自身显隐，与本项目 1.5 s 空闲计时的控制栏两套隐藏逻辑（`PlayerControl.xaml.cs:172`） |
| 与现有 `PlayerControl` 冲突 | 冲突（同上） |
| 额外风险 | 需要额外字体文件；许可不明 → 即使只用它的样式思路，也不能拷代码 |

### 4.4 `mpv-osc-modern`

| 项 | 核实值 |
|---|---|
| 原仓库 | https://github.com/maoiscat/mpv-osc-modern — **431** star（fork 90，open issues 15），**LGPL-2.1**（仓库根 `LICENSE`），最近提交 `277c6646` 2024-01-22「Create LICENSE」，`pushed_at` 2024-04-20 |
| 仍在维护的分叉 | https://github.com/Yutsuten/mpv-osc-modern — fork，1 star，最近推送 2024-10-02，LGPL-2.1 |
| 更活跃的下游 | https://github.com/Samillion/ModernZ — **1 307** star，**LGPL-2.1**，`pushed_at` 2026-06-04，自述「fork of ModernX → fork of mpv-osc-modern」。**需要活跃度时应看这个，不是 maoiscat 原仓库** |
| 定位 | 基于 mpv 内置 `osc.lua` 的「另一种 OSC 脚本」，纯外观/布局改造，feature 面比 uosc 小 |
| 是否适合进程内嵌入、无独立窗口 | **不适合**（整条 OSC，同 4.1） |
| 与现有 `PlayerControl` 冲突 | 冲突 |
| 对本项目的真正价值 | **它是 mpvw 的 `ControlBarLayout` 里 `modernx` 取值的语义来源**（`SettingsPage.Options.Program.cs:143` 的 `OptionLayoutChoice("modernx", …)`；`PlayerControl.ControlBar.cs:72/105/356-362` 用它选一套按钮分区）。即：本项目已经把 ModernX 的**布局语法**移植进 WinUI，只剩皮肤没用它的样式 |

### 4.5 `thumbfast`（以及 `osc-preview-api` 的关系订正）

| 项 | 核实值 |
|---|---|
| URL | https://github.com/po5/thumbfast |
| Star | **1 736**（fork 55，open issues 57） |
| 最近提交 | `0f711de3` 作者日期 2026-03-08、committer 日期 2026-06-28「don't strip env on non-mac platforms」；`pushed_at` 2026-08-12；默认分支 `master` |
| 许可证 | **MPL-2.0**（GitHub 识别 `mpl-2.0`；`thumbfast.lua` 文件头含「This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0」） |
| 定位 | **纯缩略图引擎，不自绘 UI**。README 原话：「The script does not display thumbnails on its own, it is meant to be used alongside a UI script that calls thumbfast」 |
| 协议 | 自己的 `script-message-to thumbfast thumb/clear` + `thumbfast-info` 回调；**不是** `osc-preview-api` |
| 致命依赖 | 它**要 spawn 一个外部 mpv 进程**产生缩略图（README「mpv frontends」节：非 ImPlay/mpv.net v7+ 的前端需要 PATH 里有独立 mpv，「It will be used in the background to generate thumbnails」）。本项目**不发布 `mpv.exe`**（§2.3 已核） |
| 是否适合进程内嵌入、无独立窗口 | **不建议引入**。要么额外分发一个 mpv 二进制（体积 + 许可 + 双进程），要么它根本不工作 |
| 与现有 `PlayerControl` 冲突 | 不冲突（它不画东西），但与本项目**已有的实现**重复：`AGENTS.md`「Thumbnail preview」节写明 seek-bar 预览是「**Thumbfast-style**：async keyframe seeks gated on `seeking` flag」，由第二个进程内 libmpv 实例做（`MpvPreviewer.cpp:132-142` 建软件渲染上下文；UI 侧 `PlayerControl.Preview.cs:80-128` + `MpvPlayerPage.xaml:1007`）。也就是说**思路已经抄过来了，而且抄得比原件更合适** |

**关于用户提到的「thumbfast 与 osc-preview-api 有关」——需要订正**：两者是不同的 API，不是一回事。

`osc-preview-api` 是 **mpv 内置 `osc.lua`** 的接口（[mpv 手册 §OSC Preview API](https://mpv.io/manual/master/#osc-preview-api)）：OSC 通过写 `user-data/osc/draw-preview`（字段 `x/y/w/h/hover-sec/ass`）请求缩略图，置 `nil` 表示清除；手册明确「避免同时装多个 thumbnailer，这个 API 只预期一个在跑」。而 thumbfast 走的是自己的 script-message 协议。实现 `osc-preview-api` 的是另一些东西，例如 NRK 的 `mfpbar`（codeberg）与 hooke007 `mpv_PlayKit` 的 `thumb_engine`——**这两个我只有搜索摘要，没有取到仓库元数据，按规矩记为「未核实」**。

对本项目的实际意义：`osc=no` 意味着 `osc-preview-api` 在本项目里**不存在消费者**，这套接口可以整节跳过。

### 4.6 外部方案小结

| 方案 | 星 | 许可 | 最近提交 | 进程内嵌入可用 | 与 PlayerControl 冲突 | 结论 |
|---|---|---|---|---|---|---|
| uosc | 3 459 | LGPL-2.1 | 2026-10-16 | 技术上可跑，产品上不该用 | **是** | **不引入**；只借它的配置面当 L1 token 清单 |
| oscc | 71 | **未声明** | 2024-10-02 | 同上 | 是 | **不引入**（许可不可分发） |
| mpv-osc-modern（maoiscat） | 431 | LGPL-2.1 | 2024-01-22 | 同上 | 是 | **不引入**；布局语义已移植（`modernx`） |
| mpv-osc-modern（Yutsuten fork） | 1 | LGPL-2.1 | 2024-10-02 | 同上 | 是 | 不引入（原仓库的不维护分叉） |
| ModernZ（活跃下游） | 1 307 | LGPL-2.1 | 2026-06-04 | 同上 | 是 | 不引入；作为未来样式参考 |
| thumbfast | 1 736 | MPL-2.0 | 2026-08-12 | **不可**（需外部 mpv.exe） | 否 | **不引入**；已有等价进程内实现 |

---

## 5. 能力归属矩阵

判断原则：**谁的数据源天然拥有它，就放谁那儿**；跨层的只写「主责 + 对方提供什么」。

| 能力 | mpv 内（Lua） | WinUI 层 | 理由 |
|---|---|---|---|
| **进度条** | 提供 `time-pos` / `duration` / `percent-pos`（已原生 observe：`MpvPlayer.cpp:304-305`） | **主责**。`PlayerControl.xaml:229` `ProgressSlider`；`PlayerControl.xaml.cs:954-960` 接线；位置更新走 100 ms 合并定时器（`PlayerControl.xaml.cs:304`） | 进度条的**外观与手感**（拖拽、吸附、DPI）只有 WinUI 能做好；mpv 侧只该给数。禁止把 OSC 的进度条也打开——那是两份 |
| **缩略图** | 理论上：thumbfast（需外部进程）/ `osc-preview-api`（本项目无消费者） | **主责**。第二个进程内 libmpv 实例软件渲染（`MpvPreviewer.cpp:132-142`），UI 侧 `PlayerControl.Preview.cs:80-128`，显示控件在 `MpvPlayerPage.xaml:1007` | 已有实现，且不依赖 `mpv.exe`；换任何 Lua 缩略图引擎都是倒退（多一个进程 + 多一份许可） |
| **章节标记** | 提供 `chapters` 元数据（`PlayerControl.ProgressMarks.cs:134` 读 `MediaPlayer.GetChapters()`） | **主责**。刻度绘制 `ProgressMarks.cs:109-152`，颜色 token `MpvChapterTickBrush`（`Styles/Styles.xaml:88-90` Light / `:162-164` Dark） | 标记要画在**自己的**进度条上，只有同层能对齐；mpv 侧画只会画在画面上 |
| **A-B 标记** | 提供 `ab-loop-a` / `ab-loop-b` 属性与命令（`PlayerControl.ProgressMarks.cs:49-68` 直接发命令；`MpvSettings.cs:319-321` 也在设置层映射） | **主责**（交互与标记）。`ProgressMarks.cs:76-104` 用 `Canvas.SetLeft` 摆 A/B 两个标记 | 循环语义归 mpv（一次性 `reset-on-next-file`，`MpvPlayer.cpp:264` 已处理），标记归 WinUI。不要让 Lua 再画一遍 |
| **字幕样式** | **主责**。`mpv.conf:237-258` 一整组 `sub-font` / `sub-font-size` / `sub-color` / `sub-outline-size` / `sub-ass-*`；设置层 `AppSettings.Subtitles.cs:8-11`（默认 42）、`:80-105`；映射 `MpvSettings.cs:255-263` 同族 | 只提供**选择器**：`SettingsPage.Options.Subtitles.cs:34-43`（字号）、`:450-457`（字体，候选来自 `SubtitleFontChoices`） | 字幕由 libass 在视频上渲染，除 mpv 外没人能改；WinUI 只该是「设置界面 + 实时下发」。**这是本项目里 mpv 侧唯一的主责外观项** |
| **OSD 提示** | **主责**。`mpv.conf:113-129` 一组 `osd-*`；`AppSettings.Osd.cs:15-24`（字号/字体）、`:81-84`（颜色）；`MpvSettings.cs:209-263` 批量映射；`show-text` 测试入口在 `SettingsPage.Options.Program.cs:186` | 提供开关与文案（元数据 OSD 由 `scripts/metadata_osd.lua` 驱动，长度上限 `AppSettings.Plugins.cs:92-95` → `PluginConfigWriter.cs:103`） | OSD 是 mpv 画在画面上的，WinUI 碰不到；反过来 WinUI 也不该自绘「提示条」——那会与 OSD 并存成两套 |
| **快捷键提示** | 理论上 OSC 会在菜单里显示 `input.conf` 的键位 | **主责**。`Modules/Player/Menu/MenuShortcutHints.cs:7-27` 从 `input.conf` 反查键位，`:8-12` 注释说明为什么用纯文本而不是 `KeyboardAccelerator`（真 accelerator 会与 `WH_KEYBOARD` 钩子双触发） | 这是本项目**已经解决的**少数跨层问题之一：`input.conf` 是唯一真源（`mpv-winui-lazy/input.conf` 176 行），WinUI 只读不写 |
| **控制栏布局** | 无（Lua 只影响自己那条 OSC） | **主责且已相当成熟**：`ControlBarAdaptiveLayout.cs:62`（`NarrowWidth = 340`）、`:151` `TierFor(width, height)`；`ControlBarLayoutEngine.cs:21-48` 纯函数重排；`PlayerControl.ControlBar.cs:70-115` 应用；`ControlBar.cs:416-468` 重建三个 `CommandBar` | 布局是窗口尺寸的函数，只有 WinUI 知道真实可用宽度/高度；mpv 侧给不了。**L2 皮肤应当只改 token 与模板，不该再复制一份布局逻辑**（§2.4 的问题正出在这里） |
| **主题色** | 无（`--osd-color` 只影响 OSD） | **主责**。`App.xaml:45-51` 七个 accent 色阶；`Styles/Styles.xaml:9-174` 两套主题字典；`SystemAccentColor2Brush` 被三套皮肤引用 | 见 §1.2 第 1 条：颜色只能走应用字典的 Color 覆盖，改 brush 无效（`App.xaml:34-40` 实测结论） |
| **字体** | 部分：`osd-font` + `sub-font`（两套，各管各的） | **部分且更宽**：`UiFont` → `Application.Current.Resources["ContentControlThemeFontFamily"]`（`WindowStyleManager.cs:54-65`），即时生效路径在 `MainWindow_Style.cs:76-83` | 字体天然分裂成三份（UI / OSD / 字幕），**不是**一份皮肤能统一的。建议在 SCP manifest 里把三者列成三个字段，明确「一个包可以只给一个」 |

矩阵读出来的一句话：**10 项里 WinUI 主责 7 项（进度条、缩略图、章节标记、A-B 标记、快捷键提示、控制栏布局、主题色），mpv 主责 3 项（字幕样式、OSD 提示，以及「字体」里的 OSD/字幕两半）**——其中「字体」按三层拆分，UI 半边归 WinUI，OSD 与字幕半边归 mpv。这正是「本项目不需要 mpv OSC」的量化版本：OSC 想抢的那几项（进度条、控制栏布局、快捷键提示）本来就已经在 WinUI 侧做得更细了。

---

## 6. 引入方式建议

### 6.1 目录结构

**先解决一个命名碰撞。** 用户提议 `Themes/<SkinName>/`，但 `Themes/` 在本项目已被框架语义占用——`Themes/Generic.xaml` 是自定义控件默认样式的约定位置（`SliderExt.cs:14`、`CheckBoxExt.cs:12` 设了 `DefaultStyleKey`，样式定义在 `Themes/Generic.xaml:6-12`），且它**不被 App.xaml 合并**、靠框架查找。把皮肤塞进同一个目录会造成两种后果：读代码的人分不清「哪些文件是框架约定、哪些是皮肤」，以及未来想给皮肤加 `Themes/<Skin>/Generic.xaml` 时会真的撞车。

两个方案，二选一：

**方案 A（推荐）—— 顶层新建 `Skins/`：**

```
mpv-winui/mpv-winui/
  Skins/
    README.md                        # 目录约定 + manifest 字段说明
    OrangeDefault/                   # 与今天的外观等价，作为回归基准
      skin.json                      # manifest
      Tokens.Light-Dark.xaml         # ThemeDictionaries（token 字典）
      Tokens.Shared.xaml             # 主题无关 token（尺寸/圆角/字体）
      ControlBar.xaml                # 可选：该包的 Standard 形态覆盖
    OceanDark/
      skin.json
      Tokens.Light-Dark.xaml
      Tokens.Shared.xaml
  Themes/
    Generic.xaml                     # 保持原样，一行都别动
```

**方案 B（贴合原提议）—— `Themes/Skins/<SkinName>/`：** 结构与 A 相同，只是父目录叫 `Themes/Skins/`。好处是「主题相关的东西都在 `Themes/` 下」；坏处是与 `Generic.xaml` 同层，仍需一份 README 解释。

两者都**不要**用 `Themes/<SkinName>/`（直接与 `Generic.xaml` 平级）——省一个目录名，换来一个长期的解释成本。

**`skin.json`（manifest）建议字段：**

```json
{
  "schemaVersion": 1,
  "id": "orange-default",
  "name": { "zh-CN": "暖橙（默认）", "en-US": "Warm Orange" },
  "version": "1.0.0",
  "author": "mpvw",
  "license": "LGPL-2.1-or-later",
  "minAppVersion": "1.0.0",
  "tokens": ["Tokens.Shared.xaml", "Tokens.Light-Dark.xaml"],
  "controlBarStyles": { "standard": "DefaultPlayerControlStyle", "centre": "CenterPlayerControlStyle", "compact": "CompactPlayerControlStyle" },
  "accentRamp": ["#FFD97525", "#FFE39659", "#FFECB78D", "#FFF4D8C1", "#FFA5591C", "#FF703D13", "#FF3C200A"],
  "fonts": { "ui": null, "osd": null, "subtitle": null },
  "requiresHighContrast": true
}
```

三个设计决定值得写下来：

- **`accentRamp` 单独列 7 个色值**，而不是让皮肤字典去覆盖 brush：因为 `App.xaml:34-40` 已经用实测证明「覆盖 brush 无效，只有这 7 个 Color 是杠杆」。manifest 里显式列出，加载器才能按 §3.2 的做法写进应用字典的主题字典**之外**。
- **`controlBarStyles` 把 L2 映射成 key 名**，而不是让皮肤自带模板文件。这样默认皮肤可以直接指向现有三个 key，零成本；新皮肤想提供第四种形态是显式声明，而不是悄悄多一份 `ControlTemplate`（§2.4 的教训）。
- **`requiresHighContrast: true`** 让加载器能拒绝缺 HighContrast 字典的包——顺带补上 `Styles.xaml` 今天缺 HighContrast 的窟窿。

### 6.2 加载顺序

启动时（在 `AppContext.Init()` 之后、`MainWindow` 创建之前，即 `App.xaml.cs:39` 与 `:44` 之间）：

1. 读当前 `Skin` 设置（新增一个 `AppSettings.Skin`，默认 `orange-default`）。
2. 打开 `ms-appx:///Skins/<id>/skin.json`；字段缺失或 `schemaVersion` 不认识 → 回落到内置包，**不抛异常**（一项外观设置不该让应用起不来）。
3. 按 `tokens` 数组顺序 `MergedDictionaries.Add(...)`，**在 `App.xaml:12` 之后追加**。恢复「`XamlControlsResources` 第一、皮肤最后」的不变量。
4. 若 `accentRamp` 非空 → 写 7 个 `Color` 到 `Application.Current.Resources`（主题字典之外，位置见 `App.xaml:45-51`）。
5. 窗口建好后再走 `RefreshPlayerStyle()`（§3.2），把 `ApplyPlayerStyle` + `ApplyControlBarStyle` 各跑一遍。

运行期换包时重复 2–5，且**必须**重跑第 5 步——理由见 §3.5 第 5 条。

**必须写进文档的限制**：皮肤只提供 token 与 style key，**不提供**数据模板、不提供 converter、不提供代码。一旦允许皮肤带 `DataTemplate`，`check-resources.py`（`tools/check-resources.py` 文件头自述「does not follow `ResourceDictionary.Source`」）就完全看不见它的引用是否有效，而 `MpvSpaceXS` 那次事故（`AGENTS.md`「交叉语言契约」节）正是「键名差一个字符 → `InitializeComponent` 抛异常 → `Frame.Navigate` 吞掉 → 空白窗口」这一类。**皮肤包是唯一一种「用户可替换、检查器看不见」的资源，这条限制是它的安全边界。**

顺带一个建议：既然 `check-resources.py` 明确不跟随 `ResourceDictionary.Source`，引入皮肤包应当同时**扩这个检查器**（让它至少遍历 `App.xaml` 声明的 Source 图）。按 `AGENTS.md` 的规矩，新检查器要先注入真缺陷看到红，再算数——最现成的注入就是把某个皮肤 token 的 key 改一个字符。

### 6.3 明确「不手搓皮肤引擎」

这一节写三条**禁止**，比写十条「应该」有用：

1. **不做脚本化皮肤。** 不引入 Lua/JS/表达式求值来驱动皮肤（例如「`if width > 1200 then …`」写在皮肤文件里）。现有 `ControlBarAdaptiveLayout` 的规则已经是纯函数并被 `mpv-conf-test` 链接测试（`AGENTS.md`「Control-bar tiers」节要求「run the tests, do not verify this one by eye」）；把布局规则搬进皮肤文件等于把它们从可测代码里挖出来。
2. **不做「第四套并列界面」。** §2.4 已经证明现有机制是「三份界面描述」而非 skinning。新增皮肤必须复用同一棵控件树，只换 token 与模板；再复制一份 `ControlTemplate`（54 KB / 36 KB 那种规模）是把这个坑挖深。
3. **不引入 OSC 作为「皮肤」。** §4 的结论已经给足理由，最硬的一条是 §2.3：发布包里没有 `mpv.exe`，thumbfast 类方案连启动都做不到。

正面的说法：**皮肤引擎 = WinUI 自带的 `ResourceDictionary` + `ThemeDictionaries` + `Style.Template`。** 这三样已经能表达本项目需要的全部（颜色阶、尺寸、圆角、字体、控制栏三种形态），再包一层只会制造「只有作者懂的 DSL」。

### 6.4 `THIRD_PARTY_NOTICES.md` 该记什么

现有文件在 `mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`（69 行，分 Scripts / Binaries / Fonts / App 字体 / VapourSynth / 配置 六节）。它的实际形态是两个表加一段 Notes，皮肤相关的内容应当：

- **皮肤包本体**：若某个 SCP 的**色板或图标**取自第三方（例如借鉴 uosc 的排版、拷贝 ModernZ 的图标），必须在 `## Skins` 新节逐包登记：`包名 | 来源 URL | 许可证 | 借用部分（仅色值 / 仅布局思路 / 含代码）`。
  - **只借色值和思路不构成衍生**，可写 `License: 无（原创配色） / Reference: <url>`；
  - **拷了 Lua/JavaScript 代码**就构成衍生，`dyn_menu.lua` 那一行（`:13`）就是现成范式：写清上游、写清是「project-local modifications」、写清许可如何界定边界。
- **字体**：SCP 若带字体，必须像 `:30-31` 那样给出**完整许可文本的文件路径**（`fonts/OFL-1.1.txt` 的做法），而不是只写「OFL-1.1」。
- **`## Notes` 里补一条边界说明**：皮肤包的资源字典在运行时被合并，与 app 代码**同进程**，因此其许可不能靠「只是数据文件」来规避——如果一个包的 token 字典是由 GPL 代码生成的，这不会改变字典本身的许可，但**生成器**的许可会进入构建链。这句话现在没有，值得加。
- **版本登记**：`scripts/VERSIONS.md` 是 Lua 的 pin 表（`:21-31`），皮肤也应当有对应的 pin——建议在 `Skins/README.md` 里给每包一行 `id | version | 本仓库引入的 commit | 上游 pin`。§4 的表格可以直接搬过去当初始行。

---

## 7. 许可证登记表

「是否已引入」= 截至 HEAD `7b42fd5` 是否已在本仓库/发布包内。

### 7.1 已引入（现状基线）

| 组件 | URL | 版本 | 许可证 | 用途 | 是否已引入 |
|---|---|---|---|---|---|
| libmpv（`mpv-2.dll`） | https://github.com/mpv-player/mpv | `v0.41.0-dev-ge8673660a`（读自 DLL 版本资源） | LGPL-2.1+ / GPLv2+ 取决于构建开关；**本 DLL 的具体构建未核实** | 播放核心（进程内嵌入） | ✅ 是 |
| `scripts/dyn_menu.lua` | https://github.com/tsl0922/mpv-menu-plugin | 未 pin（`VERSIONS.md:77`） | **GPL-2.0-only** | 右键菜单数据源（`menu-data`） | ✅ 是（本地补丁见 `LOCAL-PATCHES.md`） |
| `scripts/select.lua` / `console.lua` / `stats.lua` | https://github.com/mpv-player/mpv | 仅 `stats.lua` 有 `COMMIT_`（`VERSIONS.md:69-71`） | 见各自文件头（`THIRD_PARTY_NOTICES.md:14`） | 轨道选择 / 控制台 / 统计 | ✅ 是 |
| `scripts/coverart.lua` | https://github.com/CogentRedTester/mpv-coverart | 未 pin | MIT | 封面显示 | ✅ 是 |
| `scripts/recentmenu.lua` | https://github.com/natural-harmonia-gropius/recent-menu | 未 pin | MIT | 最近文件菜单 | ✅ 是 |
| `scripts/metadata_osd.lua` | https://github.com/vc-01/metadata-osd | 0.6.2（文件头） | MIT | 元数据 OSD | ✅ 是 |
| `MediaInfo.exe` | https://mediaarea.net/en/MediaInfo | v26.05 | BSD-2-Clause | 媒体信息解析 | ✅ 是（含 SHA-256） |
| Source Han Sans SC | https://github.com/adobe-fonts/source-han-sans | 2.otf | SIL OFL-1.1 | 字幕字体 | ✅ 是 |
| LXGW WenKai Mono Lite | https://github.com/lxgw/LxgwWenKai | Regular.ttf | SIL OFL-1.1 | 字幕/UI 字体 | ✅ 是 |
| Fluent System Icons | https://github.com/microsoft/fluentui-system-icons | Regular.ttf | MIT | 图标字形 | ✅ 是 |

### 7.2 本轮调研的外部方案（均未引入）

| 组件 | URL | 星数 | 最近提交 | 许可证 | 用途（若引入） | 是否已引入 |
|---|---|---|---|---|---|---|
| uosc | https://github.com/tomasklaen/uosc | 3 459 | 2026-10-16 | **LGPL-2.1**（`LICENSE.LGPL`） | 整套 OSC / 供 L1 token 参考 | ❌ 否（建议：仅参考配置面） |
| oscc | https://github.com/0dist/oscc | 71 | 2024-10-02 | **未声明**（无 LICENSE 文件，API `license: null`） | 暗色 OSC | ❌ 否（**许可不明，不可分发**） |
| mpv-osc-modern（maoiscat） | https://github.com/maoiscat/mpv-osc-modern | 431 | 2024-01-22 | **LGPL-2.1** | OSC 外观 | ❌ 否（布局语义已移植为 `modernx`） |
| mpv-osc-modern（Yutsuten fork） | https://github.com/Yutsuten/mpv-osc-modern | 1 | 2024-10-02 | **LGPL-2.1** | 同上 | ❌ 否 |
| ModernZ | https://github.com/Samillion/ModernZ | 1 307 | 2026-06-04 | **LGPL-2.1** | 活跃 OSC / 样式参考 | ❌ 否 |
| thumbfast | https://github.com/po5/thumbfast | 1 736 | 2026-08-12 | **MPL-2.0** | seek 缩略图引擎 | ❌ 否（需外部 `mpv.exe`；已有进程内等价实现） |
| mfpbar / thumb_engine | codeberg.org/NRK/mpv-toolbox、hooke007/mpv_PlayKit | **未核实** | **未核实** | **未核实** | 实现 `osc-preview-api` 的缩略图器 | ❌ 否 |

### 7.3 许可结论

1. **uosc / ModernZ / mpv-osc-modern 都是 LGPL-2.1**，与本项目 app 代码同许可（`AGENTS.md`「License guardrails」），许可上不冲突——**不引入的理由是产品与依赖，不是许可**。
2. **oscc 许可未声明**，是唯一一个「想用也不能用」的：无许可证的仓库默认保留全部权利。
3. **thumbfast 是 MPL-2.0**，文件级 copyleft：如果将来真要改它的代码，被改的文件必须开源，但可以与 LGPL 代码同项目共存。今天不引入的实际原因是**它需要 `mpv.exe`**。
4. **LGPL-2.1 的静态链接义务**对 Lua 脚本不构成问题（脚本是运行时加载的独立文件），但如果某个 SCP 变成「编进 app 的资源字典」，它仍然只是数据——**不过一旦 SCP 携带任何可执行生成器**，`THIRD_PARTY_NOTICES.md` 的 `## Notes` 需要补一句（见 §6.4 最后一条）。

---

## 8. 待你确认的三件事

1. **§2.4 的两分钟真机验证**（切到「居中」→ 点播放 → 拖进度条）。这条决定下一轮是做「皮肤包」还是先做「皮肤机制修复」。**在它出结果前，L1 皮肤包的工作方向都不算确定。**
2. **`Skins/` 还是 `Themes/Skins/`**（§6.1 方案 A / B）。两者只差一层目录名，但会在 README 里被引用很多次，越早定越好。
3. **`UiFont` 是否真的即时生效**（§3.5 第 8 条）。如果它其实需要重启才生效，那么能力矩阵里「字体」这一行要改写，且它会成为 `RefreshPlayerStyle` 的第一个真实用例。

## 9. 证据索引

**本地**（全部为 `git show`/`Select-String` 实取，非记忆）：`App.xaml:6-13/15-44/45-51`；`Styles/Styles.xaml:8-174`（含 `:9` Light、`:103` Dark、`:88-90` 与 `:162-164` `MpvChapterTickBrush`）；`Themes/Generic.xaml:6-12`；`Modules/Player/PlayerControl.xaml:45/147-158/229/332/366`；`PlayerControl.xaml.cs:48/94-142/486-504/899-968/903/1028/2305`；`PlayerControl.ControlBar.cs:70-115/416-468`；`PlayerControl.PanelAnimation.cs:107-108`；`PlayerControl.ProgressMarks.cs:42-68/76-104/109-152`；`PlayerControl.Preview.cs:80-128`；`ControlBarAdaptiveLayout.cs:62/151`；`ControlBarLayoutEngine.cs:21-48`；`CenterPlayerControlStyle.xaml:10-15`；`CompactPlayerControlStyle.xaml:10-15`；`MpvPlayerPage_PlayControl.cs:10-12/50-58`；`MpvPlayerPage_InputForwarding.cs:27-33/185-211`；`MpvPlayerPage_Mouse.cs:12/20-39/66-79`；`MpvPlayerPage_PlayerView.cs:13-29/58-83`；`MpvPlayerPage.xaml:999-1020`；`PiPWindow.xaml.cs:147/203-208`；`Modules/Common/View/WindowStyleManager.cs:54-65/72-81/90-98/143-146`；`Modules/Common/View/ThemeResource.cs:14-24`；`Menu/MenuShortcutHints.cs:7-27`；`mpv-winrt/MpvPlayer.cpp:246-254/264/269-273/304-305`；`mpv-winrt/MpvPreviewer.cpp:132-142`；`mpv-winui-lazy/mpv.conf:113-129/198/237-258`；`mpv-winui-lazy/input.conf`（176 行）；`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md:13-18/30-31/66-69`；`mpv-winui-lazy/scripts/VERSIONS.md:21-31/67-71/75-77`；`Modules/Settings/AppSettings.cs:166-185/296-305/308-315`；`AppSettings.Subtitles.cs:8-11/80-105`；`AppSettings.Osd.cs:15-24`；`SettingsPage.Options.Program.cs:34-70/73-93/95-115/117-131/143/186`；`SettingsPage.Options.Subtitles.cs:34-43/450-457`；`mpv-winui.csproj:192`；`tools/check-resources.py`（文件头，自述不跟随 `Source`）；`build.ps1:34/43/53/107`；`dist/mpv-winui-win-x64-Release.zip`（158 项，无 `mpv.exe`）；`obj/.../PlayerControl.g.cs`（109 个 `this.X = …`）。

**外部**（2026-10-22 实取）：`api.github.com/repos/{tomasklaen/uosc, 0dist/oscc, maoiscat/mpv-osc-modern, Yutsuten/mpv-osc-modern, po5/thumbfast, Samillion/ModernZ}` 与各自的 `/commits?per_page=2/3`、`/contents/`；`raw.githubusercontent.com` 上的 `thumbfast/README.md`、`thumbfast/thumbfast.lua`（MPL 头）、`oscc/README.md`、`oscc/oscc.lua`、`mpv/player/lua/osc.lua`、`mpv/Copyright`、`maoiscat & ModernZ /LICENSE`；`mpv.io/manual/master/` 的 On Screen Controller 与 OSC Preview API 两节；`learn.microsoft.com` 的 [XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-theme-resources)、[ResourceDictionary and XAML resource references](https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-resource-dictionary)、[Application.RequestedTheme](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.requestedtheme)。

**未核实项**（不臆测，列全）：mpv `osc.lua` 的确切许可（无版权头、`Copyright` 未逐文件列出 `player/lua/`）；本项目 `mpv-2.dll` 的构建是否 `-Dgpl=false`；`mfpbar` 与 `thumb_engine` 的星数/许可/提交；`uosc` 在本项目 composition + DPI 环境下的坐标对齐；§2.4 的运行时行为；§3.5 第 8 条 `UiFont` 是否即时生效。

---

## 10 本轮实测补充（2026-10-22）

§2.4 与 §2.5 留下的两个「需真机确认」，本轮用 `tools/mvpw-mcp/` 跑完了。两个都不是
猜测，命令与输出如下。

### 10.1 皮肤模板与代码字段：推断不成立（详见 §2.4.1）

```
python tools/mvpw-mcp/verify_skins.py
  Standard  pause False -> True   toggled=True
  Centre    pause False -> True   toggled=True
  Compact   pause False -> True   toggled=True

python tools/mvpw-mcp/verify_skin_geometry.py
  PlayPauseButton x: Standard 1655  Centre 1743  Compact 1831
  Centre/Compact 与 Standard 在全部 5 个被测控件上都不同
```

「布局会变、按钮失效」这半句不成立：布局确实变，按钮确实能用。三份并列 XAML
的**维护成本**仍然存在（§2.4.2），但那是重构理由，不是缺陷。

### 10.2 `UiFont` 是否即时生效：生效

`WindowStyleManager.UpdateUiFont()`（`:54-64`）的做法是往
`Application.Current.Resources` 写 `ContentControlThemeFontFamily`。按 WinUI
文档，运行期新增的资源对已解析的 XAML 未必有效，所以这条被标为可疑。

实测（`tools/mvpw-mcp/verify_uifont.py`，用等宽字体 Consolas 做判据 —— 同字号下
行高与字宽都与 Segoe UI 明显不同）：

| 采样控件 | 默认字体 | Consolas |
|---|---|---|
| `ShellSettingsText` 高 | **33** | **29** |
| `ShellTitleText` 高 | 38 | 38 |
| `TimeRemainingElement` 高 | 28 | 28 |

`ShellSettingsText` 的高度变了 → 该资源写入**确实**到达了活动 UI。另外两个没变，
是因为它们的 `FontSize` 与行高由各自的样式固定（`ShellTitleText` 16px、
`TimeRemainingElement` 是时间等宽标签），不是字体没生效。

**结论**：这条不是缺陷。但它的可靠性依赖 WinUI 对 `ContentControlThemeFontFamily`
的处理，属于「能用但脆」——若将来升级 Windows App SDK 后失效，判据就是上表的行高
不再变化。

### 10.3 报告里需要你定的两件事

| 问题 | 本报告的立场 | 状态 |
|---|---|---|
| 皮肤包目录用 `Skins/` 还是 `Themes/Skins/` | 推荐 **`Skins/`**。`Themes/` 已被框架约定占用：`SliderExt.cs:14` / `CheckBoxExt.cs:12` 的 `DefaultStyleKey` 指向 `Themes/Generic.xaml`，而 `App.xaml` 并不合并该字典 —— 把皮肤塞进去会和「默认样式来源」混淆 | **待你拍板**，不阻塞 |
| `SCP` 这个名字 | 建议改称 **SkinPack**：`scp` 与 Windows 自带命令、SCP 基金会三重撞名，在搜索与文档里都歧义 | **待你拍板**，不阻塞 |

两件都不影响本轮任何代码，只是命名与目录约定。
