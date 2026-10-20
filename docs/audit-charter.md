# mpvw 项目审查任务书

**状态**：草案，待你确认后才能进入实施
**编制依据**：本文件所有数字均来自对当前工作区 HEAD `0f6408e` 的实测脚本（`audit_measure.py`、`audit_structure.py`、`audit_owners.py`、`audit_owners2.py`），不是估计值
**适用范围**：`mpv-winui/mpv-winui`（WinUI 3 应用层）。`mpv-conf-test`、`mpv-winrt-test`、`mpv-winui/C++` 不在本次清理范围

---

## 0. 执行原则

**这一节优先于后面所有条款。冲突时以本节为准。**

1. **不无差别删除。** 任何文件的删除必须同时满足：全库零引用（`.cs` + `.xaml` + 资源 URI 三种机制都查过）、无隐式加载约定（如 `Themes/Generic.xaml`）、且在原位置留不下任何被其他代码依赖的类型。
2. **先证明，再修改。** 每个清理项必须附「引用证据」——是我搜过哪些模式、找到几个结果、结果在哪一行。
3. **可回滚。** 每个清理项单独提交，提交信息里写明撤销方式。
4. **规范不追溯历史。** 本任务书生效前已存在的偏离，按 §5 分级处理，不要求一次性归零。

---

## 1. 排版规范

### 1.1 页面结构（三区块）

所有主页面采用同一骨架：

```
┌──────────┬──────────────────────────────┐
│          │  内容区（滚动）                │
│ 导航栏    │                              │
│          │                              │
├──────────┴──────────────────────────────┤
│  状态栏 / 播放条                          │
└─────────────────────────────────────────┘
```

| 区块 | 宽度 / 高度 | 层色画刷 | 备注 |
|---|---|---|---|
| 导航栏 | 300–360 DIP，默认 320 | `LayerFillColorDefaultBrush` | 用 `NavigationView`，不自绘侧栏 |
| 内容区 | 剩余宽度 | `SolidBackgroundFillColorBaseBrush` | 内容内边距 24 DIP |
| 状态栏 | 内容高度 | `CardBackgroundFillColorDefaultBrush` | 播放条独立于窗口状态栏 |

**响应式断点**：沿用 `NavigationView` 的 `CompactModeThresholdWidth` / `ExpandedModeThresholdWidth`，不自定义断点。主轴窗口当前值 `CompactModeThresholdWidth="760"`（`Shell/MainWindow.xaml:163`）。

**待确认 A**：当前主窗口用 `MainWindow` 自绘侧栏，而非 `NavigationView` 折叠。是否改造为原生 `NavigationView`？改造会影响导航动画、返回栈和现有拖放逻辑，属于中等风险项。

### 1.2 间距

**规范值**：4 / 8 / 12 / 16 / 24 / 48。

**现状**：已有令牌 `MpvSpaceXxs=2`、`MpvSpaceXs=4`、`MpvSpaceCompact=6`、`MpvSpaceS=8`、`MpvSpaceM=12`、`MpvSpaceL=16`、`MpvSpaceXl=24`。

**冲突**：`Xxs=2` 与 `Compact=6` 不在 4/8/12/16/24/48 体系内。

实测非标准间距使用 **77 处 / 18 个不同值**，其中最多的：`6`(31 处)、`10`(9)、`2`(8)、`7`(7)、`28`(3)、`18`(3)。

**待确认 B**：以下三种处置，请选一。
- **B1（推荐）**：保留 `2` 和 `6` 作为**已注册的例外令牌**，在 `Styles.xaml` 注解说明用途（图标与文字的缝、紧凑行距），其余非标准值改为标准值。
- **B2**：严格执行 4/8/12/16/24/48，`2`→`4`、`6`→`8`。代价是图标与文字间距变松，列表行变高。
- **B3**：只约束新增代码，存量不动。

### 1.3 字体

**规范**：优先用官方 `TextBlock` 样式（`Caption`/`Body`/`BodyStrong`/`Subtitle`/`Title`/`TitleLarge`/`Display`），不手写 `FontSize`/`LineHeight`。

**现状**：`LineHeight` 零使用（好）；`FontSize` 手写 **99 处 / 15 个值**，其中 `34`(14)、`23`(12)、`16`(21)、`11`(6)，另有 `64`、`96`（应为 Display 级）。

官方样式已用：`CaptionTextBlockStyle`(36)、`BodyTextBlockStyle`(7)。

**处置**：这 99 处**不整体替换**。逐处判断：能用官方样式的换样式；属于图标字号/数字尺寸的（如 `64`/`96` 的封面占位）标注为图形尺寸，不当作排版字号。

### 1.4 圆角、图标、按钮

- 圆角：用 `ControlCornerRadius` / `OverlayCornerRadius` 主题资源，不写 `CornerRadius="4"`。
- 图标：用 `Segoe Fluent Icons`（`FontIcon`）。当前另引入了一套 `FluentSystemIcons-Regular.ttf`（`MpvIconFontFamily`）——**待确认 C**：是否统一为 Segoe Fluent Icons？替换需核对每个自定义字体的字形是否都有对应码位。
- 按钮：用 `AccentButtonStyle`、`SubtleIconButtonStyle` 等官方样式。现有自定义 `SquareIconButtonStyle`(20 次使用) 需评估能否映射到官方样式。

**现状偏离**：手写 `CornerRadius="4"` 出现于 `MpvPlayerPage.xaml`、`PiPWindow.xaml` 等。

### 1.5 颜色

**规范**：全部引用主题画刷，不硬编码十六进制。

**现状**：硬编码 **61 处 / 7 个文件**，集中度极高：

| 文件 | 处数 | 说明 |
|---|---|---|
| `Styles/Styles.xaml` | 29 | 阴影/遮罩的半透明黑，多为**图表层**用途 |
| `Modules/MpvConf/MpvConfTextWindow.xaml` | 22 | 等宽文本编辑器配色（`#151515`/`#7A7A7A`），肉眼可见是代码高亮色板 |
| `Modules/Player/PlayerControl.xaml` | 4 | 含 `#FFFF8C00`（橙）、`#FF00BFFF`（青）——疑为状态指示色 |
| `Modules/Player/MpvPlayerPage.xaml` | 3 | 遮罩层 `#59000000` |
| 其他 3 个文件 | 3 | 单点 |

**处置分级**：
- `MpvConfTextWindow` 的代码高亮色板 → **保留**，但迁移为具名资源并注解「代码高亮色板，非 UI 主题色」。理由：代码编辑器的语法配色不应随 Light/Dark 主题变化，官方也无对应画刷。
- 遮罩/阴影的半透明黑 → 改用 `SmokeFillColorDefaultBrush` 或具名令牌。
- `PlayerControl.xaml` 的橙/青 → **需核查用途**后再定。

**动态取色：已核查，当前不符合规范。**

`AlbumColorExtractor`（242 行）做 HSL 重饱和运算，返回 `Palette`；`ArtworkPalette`（95 行）把它转成 `new SolidColorBrush(Color.FromArgb(...))`。两者对 `ThemeResource` 的引用数均为 **0**。

也就是说封面取色完全脱离主题体系，直接产出图片的原始色。

**这是有意的设计还是疏漏，需要你判断**，因为两种改法结果差别很大：

- **D1 保持现状**：封面取色就是图片的颜色。优点是页面确实随封面变化；缺点是在浅色主题下可能得到对比度不足的文字背景。
- **D2 按规范改为主题画刷偏移**：以 `AccentFillColorDefaultBrush` 为基色，只取封面的**色相**做偏移，明度与饱和度跟随主题。视觉上更"属于系统"，但每张封面的差异会变小。

我倾向 **D2 + 保留一处例外**：律动频谱的柱体用图片色（那是"内容"，不是"界面"），页面底色与文字用主题偏移。

**待确认 D**：选 D1 还是 D2？

---

## 2. 功能页逻辑规范

### 2.1 页面清单与职责

| 页面 | 文件数 | 总行数 | 导航入口 | 返回值 | 清理位置 |
|---|---|---|---|---|---|---|
| `MpvPlayerPage` | 22 | **8943** | `MainWindow.xaml.cs` → `Navigate(typeof(MpvPlayerPage), fileItems)` | `IReadOnlyList<FileItem>` | `Unloaded`（23 条语句） |
| `SettingsPage` | 20 | **17890** | `SettingsWindow` / `SettingsPage.Actions.ImportExport` → `SettingsNavigationArgs` | `SettingsNavigationArgs` | 未实现 |
| `SettingsWindow` | 1 | 316 | `MainWindow.xaml.cs:927` → `new SettingsWindow()` | 无 | 未实现 |
| `MpvConfEditorPage` | 1 | 607 | `MpvConfEditorWindow` → `configPath` | `string` | 未实现 |
| `MenuEditorPage` | 5 | 732 | `MenuEditorWindow` → `MenuEditorParam` | `MenuEditorParam` | 未实现 |

**页面总规模：28,488 行。** 其中 `SettingsPage` 一家占 63%，是最大的单页。

> 行数用 `文件数 × 总行数` 是因为这些页面是 partial 分部类：`MpvPlayerPage` 由 22 个文件组成，
> 只看 `MpvPlayerPage.xaml.cs`（587 行）会严重低估它。

### 2.2 导航与过渡

**现状**：5 处 `Navigate` 调用，**全部未指定 `NavigationTransitionInfo`**。`ConnectedAnimation` **零使用**。

**规范要求**：页面切换用 `EntranceNavigationTransitionInfo` / `DrillInNavigationTransitionInfo`；封面到播放页用 `ConnectedAnimation`。

**处置**：这是**新增能力**而非清理。分两步：
1. 给现有 5 处 `Navigate` 补充 `NavigationTransitionInfo`（低风险）
2. `ConnectedAnimation`（音乐库卡片 → 播放页封面）作为独立需求，需单独评估

**待确认 E**：第 2 步是否纳入本次范围？

### 2.3 状态边界（跨页残留）

**全局服务**（`AppContext`，实测 14 个静态成员）：
`Logger`、`AppLang`、`AppSetting`、`RunMpvCommand`、`SetMpvLogLevel`、`GetAudioDevices`、`GetGpuAdapters`、`SendMpvCommand`、`WritePluginConfigs`、`WriteManagedMpvConfig`、`NotifySettingChanged`、`AvailableLanguages`、`Init`、`SwitchLanguage`、`WaitAll`

**页面局部状态**：`MpvPlayerPage` 家族共 **73 个私有字段**，分布：

```
Library.cs     22    Music.cs        8    Display.cs     10
Playlist.cs     9    Mouse.cs        4    PlayerView.cs   4
Info.cs         3    Preview.cs      3    InputForwarding.cs 3
FullScreen.cs   2    History.cs      2    (其余各 1–2)
```

**规范要求**：明确「播放状态 / 封面缓存 / 媒体库绑定 / 拖放播放 / 页面切换」五者的清理时机。

**已核查的两条事实：**

**1. 导航钩子是两个空方法。**

```
OnNavigatedFrom    → 0 条语句
OnNavigatingFrom   → 0 条语句
```

**2. 但清理是存在的，挂在 `Unloaded` 上，共 23 条语句：**

```
AppContext.RunMpvCommand   = null      AppContext.SettingChanged  -=
AppContext.SetMpvLogLevel  = null      AppContext.LanguageChanged -=
AppContext.GetAudioDevices = null      11 个 _mediaPlayer.* 事件取消订阅
AppContext.GetGpuAdapters  = null      TeardownLibrary()
CleanupDisplayInfo()                   CleanupPlaylistRefresh()
```

**这是一个需要你判断的结构性问题，不是缺陷。**

`Unloaded` 和"导航离开"是两个不同的事件：

| 情形 | `Unloaded` 是否触发 | 后果 |
|---|---|---|
| 导航离开且页面被缓存 | **否** | 不清理，但页面还活着，事实一致 |
| 导航离开且不缓存 | 是 | 清理，正常 |
| 页面被替换（非导航） | 是 | 清理，但页面仍是导航目标 → 下次进入需重建 |

当前 `NavigationCacheMode`：**not set (default = Disabled)**。

若为默认 `Disabled`，则每次导航都是"卸载 + 新建"，现有写法成立；若哪天改为 `Required`，这套清理会静默失效。

**待确认 J**：是否把清理从 `Unloaded` 迁到 `OnNavigatedFrom`？我倾向**保持现状 + 在代码里注明依赖**，因为改成导航钩子在缓存模式下反而会漏掉真正的卸载。

**核心交付物**：「状态归属表」（73 个私有字段逐条标注初始化/清理/触发者）需在 S3 阶段产出，是清理前的必经步骤。

### 2.4 已修复的残留问题（作为规范的反面教材）

本次会话已修复三个，写在这里作为判定标准的实例：

| 问题 | 根因 | 规范的对应条款 |
|---|---|---|
| 封面切换菜单后消失 | 标签扫描替换 entry 对象时未搬运 `Thumbnail` | 状态归属：缓存必须随对象的替换而搬运 |
| 专辑页盖在媒体库上 | `SetLibraryVisible` 只认识 `MusicHost` | 页面切换必须收敛到一个出口 |
| 禁用态闪一帧 | 四条路径各自写 `Opacity = 1` | 状态改变必须单一写入点 |

---

## 3. 组件规范

### 3.1 原则

优先官方控件。自定义前必须回答四项：必要性、复用范围、可访问性、性能影响。

### 3.2 现值盘点

**自定义 `ControlTemplate` 覆盖：6 个**——`ToggleSwitch`、`Thumb`、`RadioButton`、`AutoSuggestBox`，另加**两套播放控制条皮肤**（`CenterPlayerControlStyle.xaml`、`CompactPlayerControlStyle.xaml`，`TargetType="player:PlayerControl"`）。

**已核查：6 个模板全部合规，无需改动。**

| 文件 | TargetType | 动画内容 | 判定 |
|---|---|---|---|
| `Styles.xaml:197` | `ToggleSwitch` | 无 | 合规 |
| `Styles.xaml:340` | `Thumb` | 无 | 合规 |
| `MpvPlayerPage.xaml:531` | `RadioButton` | 仅 `VisualState.Setters` | 合规 |
| `SettingsWindow.xaml:40` | `AutoSuggestBox` | 无 | 合规 |
| `CenterPlayerControlStyle.xaml:15` | `player:PlayerControl` | **2 个 `Storyboard`** | 合规 |
| `CompactPlayerControlStyle.xaml:15` | `player:PlayerControl` | **2 个 `Storyboard`** | 合规 |

**关于那两个 Storyboard**：它们是 `FadeInThemeAnimation` / `FadeOutThemeAnimation`——**框架内置的主题动画**，放在 `ControlPanelVisibilityStates` 的 `VisualState` 里控制播放条淡入淡出。

```xml
<VisualState x:Name="ControlPanelFadeIn">
    <Storyboard>
        <FadeInThemeAnimation TargetName="ControlPanelGrid" />
    </Storyboard>
</VisualState>
```

没有 `DoubleAnimation`、没有手写 `Duration`、没有自定义缓动曲线。这正是规范 §4「隐式动画应设尽设」要求的做法，**保持原样**。

> **这条差点被判错两次**：第一版正则漏掉了带命名空间前缀的 `TargetType="player:PlayerControl"`，于是报告"4 个模板全部无动画"。若当时直接下结论，会漏掉唯一真正含 Storyboard 的两个——而它们恰好是正确的。**"全部无动画"和"全部重写动画"都是错的结论。**

**官方样式已用 30 个**，主要为 `CaptionTextBlockStyle`(36)、`SquareIconButtonStyle`(20)、`AppBarButtonStyle`(17)。

**自定义控件 40 个**，经两轮检测核实：
- **38 个有真实引用**（XAML 标签 / `new` 构造 / 资源合并）
- **1 个必需但无显式引用**：`Themes/Generic.xaml`——`SliderExt`/`CheckBoxExt` 的隐式样式，WinUI 按约定自动加载
- **1 个真正孤儿**：`ResizeGrip.xaml`（6 行 + 16 行代码），无任何引用

> **重要教训**：第一版检测脚本只搜 `.cs`，报出 38 个「无主」控件；只搜 XAML 标签时报出 12 个；两种机制（`new` 构造、资源合并）都查过后才收敛到 1 个真实孤儿。**如果按第一版结果执行，会误删 37 个在用的组件。**

### 3.3 组件审查清单（实施阶段填写）

每个自定义控件填一行：

| 控件 | 行数 | 引用数 | 能否用官方替代 | 结论 |
|---|---|---|---|---|
| （待填） | | | | 保留 / 替换 / 删除 |

---

## 4. 原生性与简洁性

### 4.1 已启用的原生能力

| 能力 | 使用处 |
|---|---|
| `ThemeTransition` | 6 |
| `RepositionThemeTransition` | 5 |
| `Mica` / 亚克力画刷 | `AcrylicInAppFillColorDefaultBrush` 等已引用 |

### 4.2 缺失的原生能力

| 能力 | 使用处 | 规范要求 |
|---|---|---|
| `ConnectedAnimation` | **0** | 封面跨页过渡 |
| `NavigationTransitionInfo` | **0** | 页面切换 |
| `EntranceThemeTransition` | **0** | 内容进入 |
| `AddDeleteThemeTransition` | **0** | 列表增删 |
| `ContentThemeTransition` | **0** | 内容变化 |

### 4.3 占位实现盘点

实测标记：`TODO` 10 处、`placeholder`(小写) 33 处、`Placeholder`(大写) 142 处、`throw new NotSupported` 4 处。

**注意**：`Placeholder` 大写多数是**输入框占位文本**属性（`PlaceholderText`），不是待办，不能计入清理目标。需按上下文区分。

**已核查：10 处全部仍有效，均不阻塞。**

| 位置 | 内容 | 判定 |
|---|---|---|
| `MpvConfEditorPage.xaml.cs:586` | `fix aot, use index??` | 真待办，AOT 相关的索引写法 |
| `MpvPlayerPage_Disc.cs:69` | `Can't open CDDA device` | 已知限制，非缺陷 |
| `MpvPlayerPage_Display.cs:33` | `use player view rect` | 重构意愿 |
| `MpvPlayerPage_Display.cs:52` | `move to Window` | 重构意愿 |
| `MpvPlayerPage_MpvMenu.cs:124` | （空） | **无效标记**，建议删除 |
| `MpvPlayerPage_MpvMenu.cs:263` | `remove & check cmd ??` | 疑问，非待办 |
| `MpvPlayerPage_Open.cs:109` | `list` | 笔记 |
| `VolumeFlyoutControl.xaml.cs:11` | `use state` | 重构意愿 |
| `VolumeFlyoutControl.xaml.cs:55` | `use state` | 同上（重复） |
| `MpvConfOptionControl.xaml.cs:293` | `TextBox SuppressEdit` | 真待办 |

**处置**：P0 级（可直接删）只有 1 条——`MpvPlayerPage_MpvMenu.cs:124` 的空 `//TODO`。其余 9 条是作者留下的重构笔记，**删除它们等于删除信息**，应保留或改写为具体任务描述。

**注意**：`Placeholder`（大写）142 处中绝大多数是 XAML 的 `PlaceholderText` 属性，不是占位实现。逐条甄别后，真正的占位实现为 0。

---

## 5. 美术与动画

### 5.1 依据要求

每条自定义美术/动画必须写明来源（官方文档链接 / 令牌名 / 开源项目具体实现）。无来源不予接受。

### 5.2 现状

**动画时长**：10 处 `Duration="0"`（瞬时的，可能用于禁用过渡）。

**自建动效系统**：`Modules/Motion/` 含 `MotionBehaviors`（Fade/Content/Poster/Settle/Row/Section/ControlState/SpringPop）、`MotionTokens`、`SpringMath`（弹簧参数 `SettleMs=280`、`Damping=0.55`、`Frequency=11.0`）。

**已核查：`SpringMath` 声明了数学模型，但未声明参数来源。**

`SpringMath.cs` 的注释给出了完整的欠阻尼振子方程：

```
x(t) = 1 - e^(-ζωt) * (cos(ωd t) + (ζω / ωd) sin(ωd t))
其中 ωd = ω * sqrt(1 - ζ²)，ζ = 1 时退化为 1 - e^(-ωt)(1 + ωt)
```

四个参数：

| 常量 | 值 | 文件中是否给出依据 |
|---|---|---|
| `SettleMs` | 280 | ✗ |
| `Damping` | 0.55 | ✗ |
| `Frequency` | 11.0 | ✗ |
| `Overshoot` | 0.14f | ✗ |

文件里唯一的依据是"让动画在 t=1 时明显结束，而不是被最后一帧切断"——这是**内部自洽性论证**，不是外部来源。

按你的 §5.1 严格判定，这属于「不予接受」。**但我不建议直接删除**，理由有三：

1. 它正在被使用（`MotionBehaviors.SpringPop` 等），删除会让动画直接消失——比参数来源不明更糟。
2. `MotionTokens` 里另有四条曲线明确标注为 Fluent 曲线，与弹簧是两套东西；弹簧只用于"回弹"这一种语义。
3. 你的规范里"柔和进入/退出/快速退出"对应的是 Fluent 的持续时间曲线；**Fluent 官方规范并未定义弹簧动画**，因此不存在可直接引用的官方参数。

**待确认 H**：三种处置，请选一。
- **H1（推荐）**：保留弹簧，在 `MotionTokens` 里补一节说明「Fluent 未定义弹簧，此参数为满足"t=1 时收敛"的工程取值」，并标注为非官方扩展。
- **H2**：删除弹簧，`SpringPop` 改用 Fluent 的 `SoftLanding` 曲线。
- **H3**：保留但现在不管，记入例外清单。

### 5.3 音乐页动效参照

你点名参考 BetterLyrics 与 Nagi。

**核查结果：无法核实，我不编造。**

我做了两次检索，返回的是**空结果集**（不是"无相关结果"）。因此我无法给出这两个项目的任何具体动画参数、时长或缓动曲线值。

按你的 §5.1「自定义美术或动画必须写明依据来源，否则不予接受」，这里的正确结论是：**这两个参照当前不可用**，而不是"参考它们实现"。

**能核实的那一半**，来自 WinUI 自身（这些是随框架发布的资源，不依赖外部项目）：

| 需要 | 官方途径 |
|---|---|
| 页面切换 | `EntranceNavigationTransitionInfo`、`DrillInNavigationTransitionInfo` |
| 元素跨页连续 | `ConnectedAnimationService` + `ConnectedAnimation` |
| 列表重排 | `RepositionThemeTransition`（**已在用，5 处**） |
| 内容进出 | `EntranceThemeTransition`、`ContentThemeTransition` |
| 增删 | `AddDeleteThemeTransition` |
| 时长/缓动 | Fluent 的 `ControlFastOutSlowInKeySpline` 等主题资源；`ThemeAnimation` 系列内置 |

**结论**：音乐页动效的改造不依赖 BetterLyrics/Nagi，可以完全靠官方 API 推进。若你希望我采用那两个项目的具体参数，需要你提供可访问的链接或本地副本。

**待确认 I**：是否接受"只用官方 API"这一路径？还是你能提供参照项目的可查来源？

---

## 6. 清理标准

### 6.1 三级分类

| 级别 | 判定 | 处置 |
|---|---|---|
| **P0 明确可删** | 零引用 + 无隐式约定 + 无复用价值 | 立即删，单提交 |
| **P1 需先确认** | 功能重复 / 被官方能力覆盖 / 疑似失效但在引用 | 列入清单，向你确认后删 |
| **P2 保留但需整改** | 有引用但违反规范 | 改造，不删 |

### 6.2 当前已知清单

**P0（1 项）**
- `Modules/Player/ResizeGrip.xaml` + `.xaml.cs`（22 行）——零引用

**P1（待你确认）**
- 硬编码颜色 61 处（按 §1.5 分三档）
- 非标准间距 77 处（取决于 §1.2 的 B1/B2/B3 选择）
- 手写 `FontSize` 99 处（逐处判断，非批量删除）
- ~~6 个 `ControlTemplate` 覆盖中的动画部分~~ → **已核查，全部合规，撤出清理清单**
- `TODO` 10 处

**P2（保留）**
- `Modules/Motion/` 全部（在用）
- `Themes/Generic.xaml`（隐式样式必需）
- `MpvConfTextWindow` 代码高亮色板

### 6.3 严禁的清理方式

- ❌ 批量正则替换硬编码颜色
- ❌ 按「看起来没用」判断文件
- ❌ 修改 `Styles.xaml` 的令牌值（影响全库，需单独评估）
- ❌ 删除 `Modules/Motion/` 下任何文件

---

## 7. 交付顺序

| 阶段 | 产出 | 阻塞条件 |
|---|---|---|
| **S1（本文件）** | 审查任务书 + 待确认项 A–I | 需你逐条回答 |
| S2 | 状态归属表（§2.3） | S1 确认 |
| S3 | 组件逐项审查表（§3.3） | S1 确认 |
| S4 | 清理执行（P0 优先，P1 逐项确认） | S2、S3 完成 |

---

## 8. 待确认事项汇总

**已由我核查完毕、无需你回答**：D（现状已查明）、F（6 个模板均合规）、G（10 条 TODO 已分类）、H 的现状部分、I（无法核实，如实报告）。

**仍需你决定**：

| 编号 | 问题 | 我的建议 |
|---|---|---|
| **A** | 主窗口是否改造为原生 `NavigationView`？ | 暂不。风险高于收益 |
| **B** | 间距冲突如何处置？ | **B1**：注册 `2`/`6` 为例外令牌，其余归标准值 |
| **C** | 是否统一图标字体为 Segoe Fluent Icons？ | 暂不。需先核对 24 个自定义字形的覆盖情况 |
| **D** | 封面取色：保持图片原色，还是改为主题画刷偏移？ | **D2**：页面底色随主题，律动柱体保留图片色 |
| **E** | `ConnectedAnimation` 是否纳入本次范围？ | 拆为独立需求，本次只补 `NavigationTransitionInfo` |
| **H** | `SpringMath` 弹簧参数如何处置？ | **H1**：保留并标注为「Fluent 未定义弹簧」的非官方扩展 |
| **I** | 音乐页动效是否接受"只用官方 API"？ | 接受。两个参照项目当前不可核实 |

---

## 9. 本任务书不做什么

- 不改动任何代码（本阶段只产出文档与清单）
- 不评价功能是否应该存在
- 不重命名文件
- 不动 `mpv-conf-test` / `mpv-winrt-test` 的既有测试
