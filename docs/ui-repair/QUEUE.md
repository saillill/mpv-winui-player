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

## 当前基线（2026-10-24 实测）

| 项 | 结果 |
|---|---|
| `verify_e2e` | 19/19 |
| `verify_settings` | 0 failure |
| `verify_settings_rows` | PASS |
| `verify_screenshot` | PASS |
| `verify_no_focus_steal` | 3/3 |
| 17 个 `tools/check-*.py` | 0 失败 |
| 构建 | 0 错误 |
| 部署 | `identical: 658 / differing: 0` |
| WUI 分析器 | 24 条，**全部已证误报** |

---

## A. 可立即开工

| id | domain | kind | repro | 症状 | evidence |
|---|---|---|---|---|---|

> 2026-10-24：A 区已清空。原来两项（`upstream-fileassoc`、`upstream-displayrequest`）
> 经两条独立测量确认能力都存在，是**探针 bug**，不是产品缺陷；探针已修并各自在注入真实
> 缺陷时变红。两项证据写在 `done.py` 记录的 evidence 里。
>
> 清空后按 PLAYBOOK 跑 `scan.py` / `nightly.py`，又发现 4 个测量问题，全部是探针的
> （见 §C 最后四行），修完仍是 0 个可开工项。**当前没有已证实的缺陷待修**——
> 这是测量结果，不是没找。

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

## D. 需要先建立测量才能立项

| 域 | 缺什么测量 |
|---|---|
| Settings | 688 行的滚动、折叠、搜索、键盘导航 |
| ~~窗口尺寸~~ | **已建**：`SetWindowPos` → 等两次连续相同尺寸（不用固定 sleep）→ 读 UIA。实测 `classic` 下限 569.7、`modernx` 720.0 DIP，与 `GetMinLogicalWidth()` 精确吻合；`Minimal` 经竖屏可达，`Narrow` 不可达（见 §C 末条） |
| Player | 三套皮肤（Centre/Compact/Standard）逐控件位置 |
| Player | 播放中/暂停/空闲三态的控制栏布局 |
| 历史与稍后看 | 列表项渲染、空态、滚动 |
| 菜单编辑器 | 树操作、拖拽、保存往返 |
| MediaInfo | 长值换行、复制、分节折叠 |
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
