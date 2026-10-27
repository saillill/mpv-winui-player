# QUEUE：缺陷台账

**这是唯一的进度依据。** 每轮从这里取一项，修完标 `done` 并写证据。

状态取值：`open` / `in-progress` / `done` / `blocked` / `cannot-reproduce` / `probe-bug`

---

## 怎么读这一页

| 字段 | 含义 |
|---|---|
| `id` | 稳定标识，`next.py` / `repro.py` / `done.py` 用它定位 |
| `domain` | 分区，决定先读哪个 README |
| `kind` | `layout` 排版 / `behavior` 功能 / `a11y` 无障碍 / `port` 移植 / `cleanup` 清理 |
| `repro` | `confirmed` 有已证复现 / `unverified` 是假设 |
| `evidence` | 复现命令或测量值。**没有证据的项不得标 confirmed** |

---

## 当前基线（2026-10-26 实测）

| 项 | 结果 |
|---|---|
| `gate.py` | **14/14 阶段通过**（其中 9 个行为验证器，每个都带 `--self-test` 证明能变红） |
| `verify_playback` | PASS（`idle=no` 根因修复后实测 `file-loaded 1`、`time-pos` 推进） |
| `verify_music_cards` | PASS（专辑/艺术家封面 stddev 13.7 / 88 色） |
| `verify_letter_rail` | PASS（视频库无字母栏；音乐库有且不重叠） |
| `verify_queue_button` | PASS（右上角打开文件按钮） |
| `verify_home_button` | PASS |
| `verify_overlay_buttons` | PASS（8/8 未被遮挡） |
| 25 个 `tools/check-*.py` | 0 失败 |
| 构建 | 0 错误 |
| 部署 | `identical 500 / differing: 0` |
| 测试 | mpv-conf-test 517 + mpv-winrt-test 29 |
| WUI 分析器 | 24 条，**全部已证误报** |

> 上一版这张表写的是 `verify_e2e 19/19`、17 个检查器、`identical: 658`，而且
> **没有 `verify_playback`** ——那份基线是在视频根本无法播放的状态下测出来的，却读起来
> 像一次全绿。9 项 UI 缺陷的逐项结论见 `STATUS-9-DEFECTS.md`。

---

## A. 可立即开工

| id | domain | kind | repro | 症状 | evidence |
|---|---|---|---|---|---|

> **2026-10-26：`overlay-lost-after-play` 撤回，它不是缺陷。** 我一度把它当成缺陷，还写了一个
> 会变红的验证器——那是**误判**，用户确认后已删除。
>
> 实际发生的事：点卡片上的播放键，界面切到播放页，音乐库与卡片**一起**隐藏。这是设计行为，
> 用户明确回答「切到播放页是期望行为，我不需要停留音乐库」。我测到的「播放后卡内控件从 4 个
> 变成 2 个」，真实含义是**卡片不在了**，不是悬浮层坏了。
>
> 错在哪：我测的是「卡内控件数」，而这个数字下降有两个完全不同的原因——悬浮层被收起，或者
> 卡片被移走。我没有先确认那张卡还在不在，就顺着第一个解释往下修了三轮。判断依据其实一直
> 拿得到：`LibraryGrid` 的矩形在播放后变成 `None`，一眼就能看出整个库已经不在屏幕上。
>
> 教训写在这里而不是丢进 git 历史：**当一个测量值下降时，先确认被测量的东西还在不在。**

> 2026-10-24：A 区当时清空过。两项（`upstream-fileassoc`、`upstream-displayrequest`）
> 经独立测量确认能力都存在，是**探针 bug**，不是产品缺陷。
>
> 更早那句「当前没有已证实的缺陷待修」在当时成立，但成立的原因值得记住：悬停行为当时
> **根本无法测量**（`SetCursorPos` 不产生 WinUI 会处理的指针消息，非前台窗口又收不到悬停
> 输入）。能测之后缺陷才浮出来。**先有测量能力，才有缺陷清单。**

## B. 已验证存在，不必移植（本会话结论）

| 声称缺失 | 实测 | 结论 |
|---|---|---|
| `upstream-appconfdata` | `ImportConfigAsync`/`ExportConfigAsync` 在 `SettingsPage.Actions.ImportExport.cs`，同文件还有 picker | **已存在**，repro PASS |
| `upstream-win32cmdline` | `App.xaml.cs` + `ActivationService.cs` 读启动参数 | **已存在**，repro PASS |
| `WatchHistoryDialog` / `WatchLaterDialog` | 用 `ContentDialog` 实现（`MpvPlayerPage_History.cs`），不是独立类 | **已存在**，形态不同 |

## C. 已验证为误报（不要再查）

| 声称的缺陷 | 实测证据 | 结论 |
|---|---|---|
| `WUI2011` ×14 `MediaInfoUserControl` | `MediaInfoRow` 只有 getter、无 `INotifyPropertyChanged`；`ShowReport` 每次重建 `ItemsSource` | `OneTime` 正确 |
| `WUI2003` ×10 `Pivot` | `find-api` 报 308 成员；运行时以 `TabItemControl` 出现在 UIA 树 | 分析器规则错 |
| `menu-data` 为空 | 无媒体时 `[]`，`loadfile` 后 24 项（`diag_menu_after_load.py`） | 时序，非缺陷 |
| 导航栏播放时消失 | 设计如此，`ShellBackButton` 恢复 | 非缺陷 |
| 左边缘竖排文字 | 窗口投影，像素 (218,219,220) | 非缺陷 |
| 排序按钮溢出 | `delta=-56` | 非缺陷 |
| 首页 4 处"间距非令牌" | 同 `Grid` 内 `VerticalAlignment="Bottom"`，14px 是字体行高 | 非缺陷 |
| 182 个 `x:Key` 孤儿资源 | `audit-resource-keys.py` 四种读取方式，0 孤儿 | 非缺陷 |
| 三套皮肤控件位置完全一致 | 几何相同但**像素不同**（`scan-skins-pixels.py`，三个不同 hash） | 无缺陷；`Template` 改的是圆角/画笔，不改控件外框 |
| 皮肤设置切换到 `Center`/`Compact` 无效 | 上一行同一个误判的起因：脚本写的是 `Centre`/`Standard`，正确值是 `Center`/`Compact`（美式拼写，见 `AppSettings.PlayerStyle_*`） | 探针 bug，不是产品缺陷 |
| 枚举选项被 mpv 拒绝（`edition` 等 59 个） | **已修** `cfb43d3`：生成器把 59 个枚举选项放进 `GeneratedEnumOptions`，`GeneratedCommand` 对它们不再加引号。这个修复本身已验证（`verify-enum-quoting.py` exit 0，27 个残留引号逐条核对 schema 全部正确）。**但原记录里的三行"实测"是错的**，见下一行 | 真缺陷，已修 |
| `mpv.conf` 里 83 处带引号的值 | **已修**，同 `cfb43d3`。枚举去引号后 83→27；其余 27 项逐条核对 schema，是 `raw`/`string` 且含逗号或空格，引号正确 | 真缺陷，已修 |
| `Failed to query swap chain's output information` x274 | mpv 的 d3d11 VO 自己报的，经日志转发。`git grep 'swap chain'` 只命中我们**避让**它的注释，不是应用代码。本轮再次确认：该字符串就在 `mpv-2.dll` 内（offset 58860825），安装型 launch 每次 2 条，与产品代码无关 | 非应用缺陷，不修 |
| 原记录："`set edition auto` 成功、`set edition "auto"` 失败、`set edition 1` 成功" | **这三条都测错了**。同一个探针把 `set volume 50` 也报成失败——它发的是 `{"command":["command",[...]]}`，多套了一层。用修正后的形状重测（2026-10-24）：`set edition auto`、`set edition "auto"`、`set edition 1` **全部成功**，说明该选项不接受"带引号=失败"这个前提，`cfb43d3` 修的是另一件事（`enum` 值进 `mpv.conf` 时的引号），那部分仍然有效 | 探针 bug，原证据作废 |
| 启动时 `CommandString rejected: set edition auto` 仍出现 | 无法复现。本轮 28 次干净启动（10 次连续 + 8 次配置重写前后 + 3 个场景 + 若干）**0 次**出现该行；ABI 直调 `mpv_command_string` 在有/无视频输出、有/无媒体三种状态下均 rc=0，且同一进程内 `set volume` 成功、`zzz-not-an-option` 失败，说明读数已校准。今日日志里修复之后（>03:14）仍有 13:46/13:58/14:07/14:12 四条，与上面的 0/28 矛盾，因此**现象存在但触发条件未知**；没有复现前不改产品 | 观察中，不改产品 |
| `scan.py` 报"4 处间距非令牌"（14/23/25/28px） | 两个独立故障：①UIA rect 是**设备像素**，`MpvSpace*` 是 DIP，本机 1.75×，所以任何真令牌都落在 1.75 倍上。控制项：`LibrarySectionSubtitle` 源码写 `Margin="0,8,0,0"`，实测 14px = 8×1.75。②`siblinghood` 用 `depth` 近似。实测 `ShellTitleText`（TitleBar 内）、`PaneRoot`（Window）、`LibrarySectionTitle`（ShellNav 内）都是 depth 4，那 23px 是标题栏到导航栏的距离。转成 DIP 后 14/28 恰好是 `MpvSpaceS`(8)/`MpvSpaceL`(16)。修后 0 条 | 探针 bug，已修 `b7b1fcf` |
| `machine-evidence.py` 报"10 个死类型" | 静态使用判定用的是子串，其中 `" %s."` 要求类型名前是**空格**；真实 C# 是 `!MotionGate.Resolve()`、`x:Bind local:TreeHelpers.MenuFor(...)`、`(LayoutDraft)x`。实测 dotted 使用数 12/8/4/2/4，检测器全部匹配 0。第 10 个 `CoverArtProbe` 是 NUnit `[TestFixture]`，靠反射发现，引用计数看不见 | 探针 bug，已修 `68b432b` |
| `scan-app-log.py` 报"14 类不同问题" | 它扫整份日文件。实测同一分钟：整份 15 条 vs 单次干净启动 3 条。多出的 12 条含 `set edition auto`（`cfb43d3` 已修）与同一选项的 FATAL——QUEUE 早已排除，只有这个脚本还在报 | 探针 bug，已修 `e7d3440` |
| `diag-edition-accepts.py` 得出"edition 根本不可设" | 请求形状多套了一层 `{"command":["command",[...]]}`，连 `set volume 50` 都"失败"，且没有控制项可暴露这一点。修正后（带成功/失败两个控制）：`edition auto`/`"auto"`/`1`/`0` 全部成功，`always` 失败（schema 的 enum 只有 no/yes/auto，符合预期） | 探针 bug，已修 `e7d3440` |
| `ControlBarTier.Narrow` 不可达（UI 缺陷候选） | **两条独立测量一致**：①实测把活动窗口 `SetWindowPos` 到 200px，外壳钳在自己的下限——`classic` 569.7 DIP、`modernx` 720.0 DIP；②`MainWindow_Size.GetMinLogicalWidth()` = 内容下限（250/400）+ `ShellNav.OpenPaneLength`（320）= 570/720，两值精确吻合。`TierFor` 先判竖屏再按宽度降序，`Narrow` 要求 `340<=w<460`，而两种布局的下限都 ≥570，所以 **`Narrow` 永不进入**；`IsTight`/`volume-slider` 的隐藏规则因此是死代码。**但 `Minimal` 可达**（竖屏分支：classic 下 569.7×571.4 实测进 portrait）。**判断：不是缺陷，不修**——`GetMinLogicalWidth` 的 +320 是 `0b17cc0` 有意的（同 commit 把导航栏宽度改成 320，并把 `CompactModeThresholdWidth` 提到 760），窗口下限高于 `NarrowWidth` 是该设计的直接后果；而且 400/250 这两个内容下限本身就高于 340，即使不算导航栏也进不了 `Narrow`。**代价已记录**：`ControlBarAdaptiveLayoutTests` 里大量断言 `Minimal/Narrow` 的用例，其中 `Narrow` 相关规则在真实窗口里不会执行 | 非缺陷；发现死规则，已记录 |

| 设置窗口 `PaneModeButton` 无无障碍名 | **真缺陷，已修** `c9371fc`。`ApplyPaneModeButtonLabel()` 是唯一设置 `AutomationProperties.SetName` 的地方，只从 `ApplyPaneMode()` 调用，而后者只从 `SettingsPage_SizeChanged` 调用——那个 handler 在宽度档位没变时**提前 return**。设置窗口默认 1316 DIP（阈值 1000），`IsNarrow` 又是 false，所以常规路径下该方法从不执行。修前实测 Name 与 HelpText 都是空，而 IsEnabled / IsKeyboardFocusable / 405×49 都正常；同窗口邻居全部正常（SearchBox=搜索、CustomizeToggle=自定义、PaneToggleButton=设置），所以不是 UIA 的锅。缩到 900 DIP 会整块换掉该按钮，来回也修不好。修后 Name=收起 |
| `CustomInput` 无无障碍名 | **真缺陷，已修** `c9371fc`。选项取自定义值时出现的自由文本框，无名字、可聚焦、195×56。改用行标签（已本地化）命名，遵循 `OptionListControl`/`OptionColorControl` 的"一处字符串"规则。修后 Name=界面字体 |
| 轨道选择器三个 Pivot 标签无无障碍名 | **真缺陷，已修** `7b61658`。四个 `PivotItem` 里只有 `SecondSubPivotItem` 设了名字，实测也只有它报名字（次字幕），其余三个报空。文件自己的注释解释了第二个为什么要修（头部是图标+两个 TextBlock，其中一个可能为空，WinUI 就不给名字）——另外三个头部形状完全相同，诊断同样成立，只是没跟着修。修后四个都能报出（字幕/次字幕/画面/声音） |
| 控制面板三个 Pivot 标签无无障碍名 | **真缺陷，已修** `7b61658`。头部在代码里用 StackPanel 拼，三个标签全空。用同一份可见文案命名。修后 audit 报 0 |
| `RadioButtonControl` 无名字（设置窗口） | **误报**。`OptionLayoutControl.xaml.cs:89` 构造选项单选钮时根本没有 Content，标题在同行相邻 `TextBlock` 里（「原版」/「居中」），读屏能读到整行。已在 audit 的 `NEIGHBOUR_LABELLED_TYPES` 里豁免并写明理由；`TabItemControl` **不**豁免，因为上面两个 Popup 的标签在自己子树里确实一个字的文本都没有 |
| 库页面能进全窗口，进去后整个窗口卡死 | **真缺陷，已修** `836bb39`。`FullWindowButton` 挂在跨页面常驻的 shell bar 上，库页面下也可点。点下去会：折叠 `TitleBarRow`/`ShellBarRow`→0、把 bar 挪到第 1 行、`ShellNav.IsPaneVisible=false`，但**页面内容仍是库**（`HandleFullWindowRequest` 只切页面 VisualState，没有播放时库后面没有视频页给 bar 浮）。四条独立测量：UIA 52 id→15（控制栏 id 全消失）；祖先链 `ShellPlayerBarSurface` 可见 1632×104、父级全部可见无裁剪（所以不是渲染问题）；截图是库页面、没有 bar；逃生路径 `ShellBackButton` 调用无效，`ShellNav_home`/`FullWindowButton`/`ShellSettingsButton`/`ShellOpenFileButton` 全部消失，Esc 与 F11 都无效——只能杀进程。**两个顺序缺陷**：①页面先翻自己的 VisualState 再问 shell，shell 拒绝时页面已经半翻（53→31、bar 消失）；②本该有的守卫不存在。修法：页面先问 `CanEnterFullWindow`，shell 在非播放页面拒绝进入；并去掉全窗口下 `OnPanelVisibleChanged` 对 `ShellBarRow` 的改高（那时 bar 已在第 1 行，改的是它不在的行）。**双向验证**：库页面 53→53 无变化、逃生路径与整条 bar 完整；播放页面仍能进全窗口、bar 在、同一按钮能退出 | 真缺陷，已修 `836bb39` |

| 高对比度完全没跟随系统 | **真缺陷，两处，已修** `071d5d9`。①`Styles.xaml` 缺 `HighContrast` 主题字典——Light/Dark 各有 12 个 key 且对称（实测 12=12，无单边 key），第三套完全没有。②`ResolveTheme()` 把 `Auto` 变成显式 `Light`/`Dark`：`RequestedTheme` 决定 WinUI 去查哪一个 `ThemeDictionaries` 分支，显式值会钉死分支，`HighContrast` 永远查不到——所以只加字典毫无效果。`Auto` 读的 `App.Current.RequestedTheme` 在对比主题下不变，于是被直接钉成旧值。**`Auto` 是默认设置，这是常规路径不是边角**。修法：`Auto` 返回 `ElementTheme.Default`（交给框架），`Dark`/`Light` 保持显式（那是用户主动覆盖系统）。**决定性 A/B 证据**（同一仪器两次运行，只有对比开关不同）：关闭 `paneBrush=#FFE4E4E4` + 仅 HighContrast 有的探针键 `NULL`；开启 `paneBrush=#FF202020` + 探针键 `Double`，而同一次运行读系统 `COLOR_WINDOW` 也是 `#202020`。**调色板故意只用一个平面色**：四套已安装对比主题里 Window 与 ButtonFace 恒等（hc1 45/45、hc2 0/0、hcblack 32/32、hcwhite 255,250,239 两次），三个底色不可能保持区分，结构改由 `NavContentBorderColor`（取 WindowText，与 Window 必然对比）承担——第一版就是拿 Window/ButtonFace 当三个面才塌掉的，是主题文件本身否掉了它 |
| `check-resources.py` 把系统色当未定义 | **工具缺陷，已修** `071d5d9`。`SystemColor*Color` 是 WinUI 提供但任何 XAML 都不声明的基色：generic.xaml 只声明配套的 `SystemColor*ColorBrush`，`Color` 形式仅以 734 处 `Color="{ThemeResource SystemColor...}"` 出现，所以从 generic.xaml 建的允许表永远收不到它们，五个真 key 被判未定义。新增 `SYSTEM_THEME_COLOURS`，并用三次注入验证按名精确匹配、不是前缀匹配：错拼 app key、`SystemColorWindowColour`、无 `System` 前缀名，3/3 变红 |

| 控制栏皮肤设置完全没有视觉效果 | **真缺陷，已修其可实现部分** `7ac7487`（`6bc68fa` 修工具）。设置页许诺「Compact 收窄控制栏并去掉音量滑条」（`en-US.json:1470`），实测三套皮肤渲染的是**同一棵内联树**。机理已确证：`ApplyPlayerStyle()` 确实执行、确实把带 `Template` setter 的 `Style` 赋给 `PlayerControl`，但插桩显示 `styleSet=True styleTplNull=1`（Style 有一个 setter，就是 Template）而 `tplNull=True`——**`Control.Template` 从未被填充**。原因：`PlayerControl` 是 `UserControl`，内容由自己的 XAML 经 `InitializeComponent` 提供，WinUI 不把 `Style` 的 `Template` setter 应用到 `UserControl`。三条旁证：SDK generic.xaml 里 `TargetType` 为 `UserControl` 的样式数为 **0**；本仓库真正生效的模板化控件（`SliderExt`/`CheckBoxExt`）都靠 `DefaultStyleKey`，`PlayerControl` 从未设置；`Themes/Generic.xaml` 只覆盖这两个 `Control` 子类。**修法选择**：不启用皮肤模板——它们已过期，79 个被代码引用的名字里 **39 个在两个模板中都不存在**，连 Center 都缺 `ControlPanelButton` 与 `PiPButton`，启用等于删功能。改为实现设置页真正许诺的那一条：`ControlBarAdaptiveLayout.SkinHides` 作为第三个可见性输入接进 `ApplyControlBarVisibility`（与已有的 `_audioOnly`、`volume` 合并同形），纯函数、可在 `mpv-conf-test` 断言。**双向验证**：Compact 音量滑条消失且静音键仍在，Default/Center 都保留滑条，三者 transport 均可用；单测 496→500，`verify_skins.py` 由 exit 1 变 exit 0。**未做**：Center 的「控件居中」是位置承诺而非隐藏规则，内联控制栏本来就居中运输控件，要断言差异需几何测量，留待后续 |

| 设置页键盘无法遍历选项行 | **真缺陷，未修（需定方案）**。实测（窗口已确认在前台、每一步都复查前台未丢失）：用鼠标直接点选选项控件能把焦点落到 `ListItemControl '紧凑'`，说明**行本身可用指针操作**；但键盘从该行起按 Tab，焦点立刻弹出，14 步只在 6 个外围控件（搜索 / 一个 ComboBox / 自定义 / 重置当前分类 / 重置所有设置 / 设置）里循环，**永远回不到任何选项行**。原因已定位到 `OptionListControl.xaml:448` 把 `OptionListViewItemStyle` 的 `IsTabStop` 设为 `False`（该样式在 `:560` 作为选项 ListView 的 `ItemContainerStyle`）。**实验结论**：改成 `True` 后 Tab 确实能停到一个 `ListItemControl` 上（`chromeOnly` 由 true 变 false、`listItemFocused` 变 true），但仍到不了行**内部**的控件；再叠加 `TabFocusNavigation=Once/Local/Cycle` 三种取值都不改变这一点（三次重建部署都测了）。**当前状态**：实验已全部回滚，`OptionListControl.xaml` 与 HEAD 一致。**未定的方案**：把行容器做成单一 Tab 停靠点并让方向键在行内移动（需要自己实现行内导航），或让行内控件直接进 Tab 序列（要考虑 `CanReorderItems` 的拖拽起点是否被改变）——两条都需要先想清楚交互再改，不在本轮猜 |
| 设置页探针的四个坑（写下来免得重踩） | ①设置是**独立窗口**、标题是「设置」，按窗口句柄顺序取"最新那个"会拿到主窗口 `MPV`，第一版因此测出 111 个元素、焦点停在「主页」、没有任何可滚动容器——全是主窗口的数据。②窗口创建在 -32000 屏外，**移动它之前任何坐标都没有意义**。③`SetScrollPercent` 可以返回成功而 `VerticalScrollPercent` 纹丝不动，所以"调用了滚动 API"不能当作"滚动了"。④`SendKeys("{TAB}")` 之后若窗口失去前台，Tab 会打进任务栏（实测焦点跑到「开始」「Everything 已固定」），于是"只循环 6 个控件"的结论可以用错误的原因得出——必须每一步复查 `GetForegroundWindow()`。另外 `GetFocusedControl()` 在焦点切换瞬间会抛 `COMError`，不加重试会让整条 trace 中途断掉 |

| Customize 模式探针的三个坑（写下来免得重踩） | ①`CustomizeToggle` 暴露的是 **`TogglePattern` 而非 `InvokePattern`**：`Invoke` 或"按可访问名点一个按钮"都没把 `CustomizeRoot` 打开，第一版因此报出「0 个文件夹 / sameSet=true」——空集对空集的比较，什么都不能证明。②进入 Customize 会调 `RebuildLocalizedContent()`，**toggle 自己从树里消失**、由 `CustomizeExitButton` 接管；切完再按原句柄找 toggle 会 `IndexError`，正是"朴素往返"会踩的地方。③在窗口尚未完成布局时调 `Toggle()` 会抛 `COMError`（"未指定的错误"），必须等到窗口矩形稳定后再调 |
| 菜单编辑器/历史这两个域的入口限制 | 播放页专属的三个面（`MenuEditorWindow`、`open-watch-history`、`open-watch-later`）都只能从**播放页**进入：`MainMenuBar` 在库页面被 `Visibility.Collapsed` 且 `MenuRow` 压成 0（`MpvPlayerPage_Library.cs:2800-2802`），右键菜单挂在 `PlayerView` 上；而 IPC `loadfile` **不会**把页面切到播放页（只有用户自己的打开路径会，`_forceShowVideo`）。**要覆盖这三个面，需要先造一个用户态打开文件的自动化路径**（拖放注入或 Open 对话框），这本身是新的测量手段，不是改产品 |

| 验证时窗口不停跳到前台 | **分两部分：测量侧已修，产品侧未修（已定位到确切阶段）**。①**测量侧（真凶，已修 `47130a7`）**：`ImageGrab.grab(bbox=)` 读的是桌面，所以每个截图探针都必须把窗口从 (-32000,-32000) 拖到屏内——这才是"验证一直跳"的来源。改用 `PrintWindow(PW_RENDERFULLCONTENT)` 渲染到自建 bitmap，**窗口可以一直停在屏外**；实测停在 -32000 时照样得到完整 2880×1536 的真实界面（库网格/导航/搜索/控制栏齐全）与设置窗口 2304×1228。`screenshot_region()` 默认走这条路并回报用了哪种方法。端到端证据：一次模拟真实会话（启动/快照/点控件/开设置/进自定义）共 **139 次有效采样，0 次在屏、0 次抢前台**，3 张截图全部 PrintWindow 且非空白。②**产品侧（未修）**：逐步隔离后确认**只在"打开设置窗口"这一步**抢前台（启动本身不抢，`foregroundIsApp=false`），且**发生在 `new SettingsWindow()` 构造阶段**——在 `ShowWindow()` 里插桩后日志一行都没有，说明它根本没被调用；随后前台已从外部应用（2427420）变成设置窗口 HWND。**我试过三种修法全部证伪并已回滚**：给 `ShowWindow` 的 `SetForegroundWindow` 加 `OwnsForeground` 守卫、把 `AppWindow.Show()` 换成 `SW_SHOWNOACTIVATE`、用 `SetWindowLongPtr(GWLP_HWNDPARENT)` 设 owner（实测 owner 确实设上了、`GetWindow(child,GW_OWNER)` 与主窗口一致，但**仍抢前台**；而且 owner 改动**破坏了设置窗口使其无法打开**，回滚后恢复）。**结论：这是 WinUI 3 创建顶层 `Window` 时的固有激活行为，不是本仓库某行代码可关掉的开关。**下一步若要真修，需要把设置改成主窗口内的页面/`ContentDialog`，或用 Win32 创建窗口后挂 `DesktopChildSiteBridge`——都属于结构改造，需先定方案 |

## D. 需要先建立测量才能立项


| 域 | 缺什么测量 |
|---|---|
| Settings | ~~688 行的滚动、折叠、搜索、键盘导航~~ **滚动已建** `_work/probe_settings_nav.py`：扩展区 9.4%，`SetScrollPercent(100)` 后百分比 0→100 **且**顶部可见行从「程序/播放器外观…」变成「程序/窗口与界面…」两处同时变化才算滚动成立（只调 API 不改视图不算）。**键盘有问题，未修**：见 §C 末条。**搜索**由 `verify_settings.py` 覆盖。**折叠**由 §A 已修的三处同步（`Option.AdvancedSection` / `FoldedSections` / `OptionListControl.CollapsedSectionIds`）覆盖。**注意**：这个域一个探针踩了四个坑，全部记在 §C 末条，下次直接看那里。 |
| ~~窗口尺寸~~ | **已建**：`SetWindowPos` → 等两次连续相同尺寸（不用固定 sleep）→ 读 UIA。实测 `classic` 下限 569.7、`modernx` 720.0 DIP，与 `GetMinLogicalWidth()` 精确吻合；`Minimal` 经竖屏可达，`Narrow` 不可达（见 §C 末条） |
| Player | ~~三套皮肤逐控件位置~~ **已建** `_work/probe_skin_markers.py` + `tools/mvpw-mcp/verify_skins.py`：先关应用再写注册表（退出时 `Flush()` 会用内存值覆盖，写早了等于没写），再加载媒体确认 transport 在，然后比对"只存在于某个模板、内联树里没有"的交互控件名。**结论不是位置问题**：三套皮肤渲染的是同一棵内联树，皮肤设置完全没有视觉效果（见 §C 末条）。**注意**：这个域不能用截图判断——本机 `BackdropType=Mica`、veil 带 alpha，且窗口默认在 -32000 屏外，本次会话有两次"截图证明"实为桌面壁纸。 |
| Player | ~~播放中/暂停/空闲三态的控制栏布局~~ **已建**：三态各 6/6 控件可用，且窗口态下控制栏本就不该隐藏（`SetOverlayMode(true)` 只在 PiP / 全窗口 / 全屏调用）。**未覆盖**：从播放页进入全窗口后 1.5s 浮层自动隐藏——唯一入口是用户自己的打开文件路径，IPC 驱动做不到 |
| 历史与稍后看 | **结构已审，运行时未测**（入口够不到，原因见下）。①**列表项渲染**：两者都是 `ContentDialog` + 代码构造（`MpvPlayerPage_History.cs:14/33`），行模板在 `WatchHistoryControl.xaml:49-94` / `WatchLaterControl.xaml`。②**空态：逻辑成立且分三种**——`UpdateEmptyState()`（`WatchHistoryControl.xaml.cs:69-96`）按 `Items.Count==0` 切 `HistoryListView` 与 `EmptyTextBlock` 的可见性，两者严格互斥；再按「历史文件不存在但开关开着」→`WatchHistoryEmpty`、「开关关着」→`WatchHistoryDisabled`、「文件存在但没条目」→`WatchHistoryNoEntries` 三选一，不是一句笼统的空提示。③**滚动**：两个控件都是 `ListView`（自身可滚），内层无嵌套滚动。**为什么没跑运行时**：唯一入口是播放页 File 菜单的 `open-watch-history`/`open-watch-later`（`MpvPlayerPage.xaml:771/775`）或 `PlayerView` 的右键菜单（`MpvPlayerPage_MpvMenu.cs:68-69`），而 `MainMenuBar` 在库页面被 `Visibility.Collapsed`、`MenuRow` 同时压成 0（`MpvPlayerPage_Library.cs:2800-2802`）；IPC `loadfile` 又**不会**把页面切到播放页（只有用户自己的打开路径会）。本机 `watch_later` 目录当前 0 条目，因此即便拿到对话框也只会看到空态。**要测需要**：一个用户态打开文件的自动化路径（如拖放或 Open 对话框注入），这本身是新的测量手段 |
| 图标规范 | **已建检查器并修完确定的缺陷**（check-icon-standard.py，纳入 gate）。先纠正一个我自己犯的错：图标是**两套系统**——框架符号字体 115 处（不写 FontFamily，用 Segoe 码点，合法）+ 打包的 FluentSystemIcons 33 处（MpvIconFontFamily）。我第一版要求统一成一套，报出 74 条里 58 条是误报，规则已删。真正测出的：①**错字形 4 处**——Segoe 码点被放进 Fluent 字体。U+E768 在 Fluent 是 line_48（一条线）、在 Segoe 是"播放"，却挂在三个播放按钮下（CardPlayButton/AlbumCardPlayButton/AlbumPlayAllButton）；U+E710 在 Fluent 是 home_32、挂在"添加"按钮下。已换 U+F605(play_20)/U+F109(add_20)。②**硬编码字体名 3 处**——FontFamily=""Segoe Fluent Icons""（CenterPlayerControlStyle.xaml + OptionListControl.xaml ×2），同文件另外 8 个 FontIcon 都是继承的，只有这 2 处是例外；已改 SymbolThemeFontFamily。③**未 token 化尺寸 2 处**——PlayerControl.xaml 的 ShuffleSymbol/PiPSymbol 写 FontSize=""20""，同文件其余 16 处都用 MpvTransportIconSize。**一个必须记住的教训**：打包字体覆盖了私有区**全部 6400 个码点**（fontTools 实测，全部是真 ic_fluent_* 名字），所以"码点是否存在于字体"是**永不触发的检查**，我写出来后删掉了，改用字形名里的设计尺寸做判据。剩 51 条（13 个文件里与图标尺寸 token 等值的字面量）进基线与 gate 共存，且已验证：注入 4 条新违规会红、51 条已知不会掩盖 |
| 排版规范 | **已建检查器**（check-typography-standard.py，纳入 gate）。两套 token 都完整：间距 MpvSpaceXxs/Xs/Compact/S/M/L = 2/4/6/8/12/16；字号 MpvPageHeadingFontSize 40、MpvPanelTitleFontSize 20、MpvShellTitleFontSize 16、MpvPopupLabelFontSize 14、MpvPopupTitleFontSize 12、MpvTransportTimeFontSize 12、MpvPopupCaptionFontSize 11；行高 MpvBreadcrumbLineHeight 32。实测 **93 条**与 token 等值的字面量（FONT-LITERAL 24 + SPACING-LITERAL 69），分布在 20 个文件。**已清 76 条，剩 17 条判定不改**（提交 9398df1）。分界由**类型**决定，不是口味：Spacing/RowSpacing/ColumnSpacing/FontSize 是 double，MpvSpace* 与字号 token 都是 x:Double，替换类型正确；Margin/Padding 是 Thickness，裸 double **不是** Thickness 且 XAML 不会隐式转换，替过去是**编译错误而不是修复**。剩 17 条不改的实测理由：Styles.xaml 里**没有**尺度命名的 Thickness token（现有的全是角色名 MpvCardPadding/MpvActionButtonPadding），加一套等于引入新概念；而且 CompactPlayerControlStyle.xaml:31 的注释写明「Padding=""4"" is 4 on every side, so 8 vertical, plus 28 (progress/time) + 60 (transport)」——这个数字参与高度预算，换成 token 只是把它藏起来，并没有解耦。这**不是**"没人遵守的规则"：仓库已有 **129 处** MpvSpace* token 用法，比对侧字面量 69 处，比例 1.87:1 —— 是真实的同类型不一致。判据刻意收窄：①Margin=""8,0,8,0"" 这类简写**不报**，因为把其中一个数字换成资源要重写整个属性，而简写表达的是一种关系（横 8、纵 0），token 表达不了；报它会产生几百条作者理应无视的噪声，那正是教人无视检查器的做法。②只有 TextBlock/TextBox 等文本元素的 FontSize 才归排版管，图标字号归图标检查器。93 条进基线与 gate 共存；已验证注入 Spacing=""8"" 和 TextBlock FontSize=""16"" 都会红、恢复后绿。**顺带量到但判定为合理**：全仓库 20 处硬编码颜色，其中 18 处在 MpvConfTextWindow.xaml（mpv.conf 代码查看器，自带固定深色配色是它的设计），另 2 处是 PlayerControl.xaml 的 AB 循环 A/B 标记色 #FFFF8C00/#FF00BFFF（功能性配色，非主题色）。两处都不改 |
| 菜单编辑器 | **已建** `_work/probe_menueditor.py`。先分清**有两个编辑器**：`Modules/Menu/MenuEditor/MenuEditorWindow`（编辑 `menu.conf`，只从播放页菜单开，够不到）与设置页 **Customize 布局**（书签管理器形状，库页面可达）——测的是后者。①**树操作**：实测 6 个 `TreeItemControl`，名字为 程序/播放/画面/声音/字幕/界面，全部 `enabled=true`。②**拖拽**：XAML 声明 `AllowDrop` + `CanDragItems` + `CanReorderItems` + 双横条拖拽手柄（`SettingsPage.xaml:491-497` + 手柄注释），但 **UIA 不暴露 `DropTargetPattern`**（`GetDropTargetPattern()` 抛 `AttributeError`），所以"拖拽可用"只能由声明+手柄佐证，**不能由 UIA 断言**；这是 WinUI TreeView 的暴露限制，不是缺陷。③**保存往返**：两轮都通过——退出再进 Customize 后文件夹集合 `sameSet=true`（6→6），**关掉整个设置窗口重开**后仍 `sameSet=true`（6）。比较用集合**且**比数量，避免空对空蒙混。**注意**：`FolderTree` 的 UIA `AutomationId` 实测是 `'ListControl'` 而不是 `FolderTree`（`TreeView` 默认如此），按 aid 找会漏掉 |
| MediaInfo | ~~长值换行、复制、分节折叠~~ **已建** `_work/probe_mediainfo.py`。①**长值换行：成立**，`MediaInfoRowTemplate` 的 value 列 `TextWrapping="Wrap"` + `IsTextSelectionEnabled="True"`（`MediaInfoUserControl.xaml:30-36`），换行高度可测。②**复制：成立**，每张卡有 `CopySection_Click`（`:88-91`），底部有 `CopyAllButton`（`:184-186`）。③**分节折叠：不存在**——`Modules/MediaInfo/` 全目录 `grep -iE "Expander|IsExpanded|Collaps|ToggleButton|Chevron"` 只命中的是 `Visibility="Collapsed"` 和加载态的显隐，**没有任何展开/折叠机制**。D 区这一格原本把"分节折叠"列为待测项，实测结果是"该功能没有"，不是"没测"。**入口**：`MediaInfoButton` 在播放列表面板里，只有可访问名（"媒体信息"/"Media info"）**没有 AutomationId**，`MediaInfoWindow` 也不设 `AutomationId`/**不用 `MPV` 标题**（`AppWindow.Title = lang.MediaInfoTitle`），所以按 id 或按标题都找不到它——要用可访问名定位。 |
| 无障碍 | ~~8 状态 × 238 交互实例复测~~ **已建** `scan-a11y-surfaces.py`：主窗口 + 设置窗口 + 控制面板三处走遍，按"有没有名字"和"是否靠邻居 TextBlock 标名"分开统计，元素数为 0 的界面报 UNMEASURED 而非通过。修完四处真缺陷后，三处均 0 bare（见 §C 末四行）。**未覆盖**：`MoreButton` 打开的 More 菜单在 Wide 档不存在（`HasOverflowContent` 为假，设计如此），要测它得先把窗口收到 Standard 档 |
| 高对比度 | ~~主题切换后对比度采样~~ **已建** `_work/probe_hc_verify.py`：切系统对比主题 → 重启应用 → 读应用实际解析出的 brush，并与同一次运行读到的系统 `COLOR_WINDOW` 比对；对比主题开启前后各跑一次作为 A/B 对照，结束时恢复原设置并回读验证。**修完两处真缺陷后**：关闭时 `paneBrush=#FFE4E4E4` 且 HighContrast-only 探针为 `NULL`，开启时 `paneBrush=#FF202020` 且探针为 `Double`（见 §C 末两条）。**注意**：不要用截图像素做这个判断——本机 `BackdropType=Mica`，而 shell 的 veil brush 带 alpha，像素是叠在桌面壁纸上的合成值，实测读到过 `#4F5456`/`#BAC5D1` 这类与任何 token 都对不上的数。 |

---

## 决策记录

### 2026-10-24 建立本任务

- **决定**：不修 `audit_domains.py`，在 `tools/ui-repair/` 建独立工具链
- **依据**：它一轮会话里四次把探针问题归因给产品；八域共享一进程而 IPC 每进程只服务
  一个连接，是结构性问题
- **放弃**：让它兼任验证器

### 2026-10-24 repro 判据必须是行为，不是名字

- **决定**：`repro.py` 全部改为查"能观察到应用在做这件事"，不查"上游用了什么标识符"
- **依据**：第一版四个 repro 全按类型名 grep，全部报"缺陷存在"。其中
  `SettingsPage.Actions.ImportExport.cs` 这个**文件名**含 `ImportExport`，而**内容里
  一次都没有**——它是 `partial class SettingsPage`，方法叫 `ImportConfigAsync`。
  按名字判断会让人去移植一个已经存在的功能
- **代价**：判据变宽（`ImportConfig|ExportConfig|AppConfData` 任一命中即算存在），
  可能放过部分实现。接受：宁可漏报，不可误报后白改

### 2026-10-24 间距审计口径

- **决定**：改为"同 `depth` 且竖直相邻"，并排除框架自绘控件
- **依据**：不加 `depth` 时把跨层级的 23px / 39px 报成问题
- **放弃**：`parentId`——snapshot 里没有这个字段

### 2026-10-24 几何相同不等于视觉相同

- **决定**：皮肤一致性改用像素比较（`scan-skins-pixels.py`），不用控件 rect
- **依据**：三套皮肤的控件 rect **完全一致**，但窗口像素是**三个不同 hash**。皮肤
  样式改的是 `Template` 内的圆角、画笔、按钮 chrome，控件外框不变
- **教训**：rect 只能证明"控件在哪"，不能证明"控件长什么样"。拿它判视觉一致性会得出
  "三套皮肤一模一样"，而事实相反
- **附带发现**：第一次写的皮肤脚本用了 `Centre`/`Standard`，而常量是
  `Center`/`Compact`，于是两次落在 `default` 分支、量到的是同一个皮肤。这类"探针自己
  设错了参数"的误判在本仓库已出现多次，写测量脚本时必须先确认参数取的是**代码里的
  常量**，不是记住的名字

---

## 格式约定

```
| `id` | domain | kind | confirmed/unverified | 一句话症状 | 复现命令或测量值 |
```

`unverified` 的项**不得**直接改产品代码——先按 `PLAYBOOK.md` 第 1 步证明它。
