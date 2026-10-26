# 9 项 UI 缺陷：逐项状态

**这是本轮（2026-10-26）对这 9 项的实测结论。** 每一项都附可重跑的命令；
没有可重跑证据的项明确标注为未证，不冒充已修。

状态取值与 `QUEUE.md` 一致：`done` / `cannot-reproduce` / `partial` / `open`。

---

## 摘要

| # | 缺陷 | 状态 | 证据 |
|---|---|---|---|
| 0 | **MPV 无法播放视频**（前置条件） | `done` | `verify_playback.py` |
| 1 | 首页「打开文件」按钮位置太靠上 | `done` | `verify_home_button.py` |
| 2a | 悬浮按钮被遮挡 | `cannot-reproduce` | `verify_overlay_buttons.py` |
| 2b | 悬浮/菜单按钮图标不正确 | `done` | `check-more-glyph.py` |
| 3 | 音乐库封面显示为长条灰色底 | `done` | `verify_music_cards.py` |
| 4 | 专辑与艺术家两个图片均未显示 | `done` | `verify_music_cards.py` |
| 5 | 字母排序出现诡异重叠按钮 | `cannot-reproduce` | `verify_letter_rail.py` |
| 6 | 字母排序应悬浮放大 + 波浪渐变 | `partial` | 见 §6，**本机不可观测** |
| 7 | 视频库应显示大路径 + 分栏 | `done`（分栏 `open`） | 见 §7 |
| 8 | 视频库不需要右侧字母索引 | `done` | `verify_letter_rail.py` |
| 9 | 播放队列需要右上角「添加文件」 | `done`（点击行为未证） | `verify_queue_button.py` |

一次跑完全部行为证据：

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv
python tools/ui-repair/gate.py     # 期望 14/14
```

---

## 0. MPV 无法播放视频 —— `done`

根因：启动批次第 152 条 `no-osd set idle no`，覆盖了 `MpvPlayer::Initialize` 刻意的
`idle=yes`。内嵌播放器启动时永远没有文件，`idle=no` 下 mpv 退出播放循环，之后
`loadfile` 返回 0 而什么也不做。

完整记录（含二分过程与为什么此前 20+ 个排除项都没找到）：`PLAYBACK-FAULT-2026-10-26.md`。

```bash
python tools/mvpw-mcp/verify_playback.py            # VERDICT: PASS
python tools/mvpw-mcp/verify_playback.py --self-test # SELF-TEST PASSED
```

实测：`file-loaded 1`、`surface 1280x720`、`time-pos 12.26 → 13.45`、`pause=false`。

---

## 1. 首页「打开文件」按钮位置 —— `done`

原来按钮在标题区**上方**独立一行；音乐库的「添加文件夹」在标题行右端。现改为同一位置。

```bash
python tools/mvpw-mcp/verify_home_button.py   # VERDICT: PASS
```

并删除了 shell 里那个常驻的旧按钮（`UpdateShellOpenFileButton` 无条件 `Collapsed`），
避免同一动作出现两次。

---

## 2a. 悬浮按钮「处于被遮挡状态」 —— `cannot-reproduce`

报告说图标被遮挡。实测 8/8 按钮在自己像素上可命中，`covered: none`：

```bash
python tools/mvpw-mcp/verify_overlay_buttons.py   # VERDICT: PASS
```

测量方法：对每个按钮取自己的中心与四角共 9 个采样点，用 `ControlFromPoint` 判断命中的
是不是它自己（用几何包含比较，不用 `id`——`uiautomation` 每次返回新的包装对象，
`id()` 比较必然失败，这曾让 8/8 全部误报为 COVERED）。

**没有复现的缺陷不修**，也不假装修了。若在实际使用中仍看到遮挡，需要一张截图说明
挡住它的是什么窗口——遮挡可能来自本机而非应用。

---

## 2b. 悬浮/菜单按钮图标不正确 —— `done`

`MoreButton` 用的是 `&#xE10C;`（汉堡菜单），应为 `&#xE712;`（三点）。两处
（`CenterPlayerControlStyle.xaml`、`CompactPlayerControlStyle.xaml`）都已改，并加
`SymbolThemeFontFamily`。

```bash
python tools/check-more-glyph.py   # OK: 2 More button(s) across 39 XAML file(s) use &#xE712;
```

这个检查器自身曾有三个缺陷（`REPO` 层级错、正则要求 `#` 而实际是 `&#x`、把 `obj\` 下的
副本也算进去），其中两个是靠"解析到 0 项就 exit 2"兜住的——一个永不匹配的正则否则会
永远报成功。

---

## 3 + 4. 音乐库封面 / 专辑与艺术家图片 —— `done`

两者是同一个问题：卡片里有没有图，还是只有一块纯色。`assert_image_content` 用两个数字
回答。

| 视图 | 修复前 | 修复后 |
|---|---|---|
| 歌曲 | stddev 17.1 / 94 色 | 不变 |
| 专辑 | **stddev 0.0 / 1 色** `rgb(32,32,32)` | **13.7 / 88 色** |
| 艺术家 | **stddev 0.0 / 1 色** `rgb(32,32,32)` | **13.7 / 88 色** |

```bash
python tools/mvpw-mcp/verify_music_cards.py   # VERDICT: PASS
```

**三个独立成因**，都是"两件事约定了契约、没东西比对、断裂是静默的"：

1. **标题绑定为空**：模板用 `{Binding}`（因为 `{x:Bind AlbumTitle}` 会让 WindowsAppSDK
   2.3.9 编译器崩在 WMC9999；这个取舍在模板注释里写着，我复现确认了）。但
   `ItemsRepeater` 与 `GridView` 不同，**不会给实例化的元素设 `DataContext`**，于是
   `{Binding}` 全部解析成 null。手工设好 `DataContext` 后两个 `TextBlock` 仍然为空——
   `{Binding}` 在这个模板里根本不生效。改为在 `ElementPrepared` 里直接写文本。
2. **媒体区没有高度**：`Height="{Binding ActualWidth, RelativeSource={Self}}"` 同样是
   不解析的 `{Binding}`；`{x:Bind ActualWidth}` 又会崩编译器。改为代码设置高度。
3. **缩略图不落地**：缩略图确实加载了（日志记录 `.flac` 得到 `600x600`），
   `PropertyChanged` 也发了，但 `{x:Bind Thumbnail, Mode=OneWay}` 没有更新 `Image.Source`。
   改为命名元素 + 一次性 `PropertyChanged` 监听（缩略图是卡片实例化之后异步到达的）。

`FrameworkElement.FindName` 也进不去已实例化的 `DataTemplate`（实测每张卡片都报
"no AlbumCardCoverImage on Button"，而元素确实在树里），所以查找改为遍历可视树。

---

## 5. 字母排序「诡异重叠的按钮」 —— `cannot-reproduce`

实测字母栏是 27 个**单字母**按钮，32 px 等距堆叠，**没有任何重叠**：

```bash
python tools/mvpw-mcp/verify_letter_rail.py   # VERDICT: PASS
```

验证器同时断言：每个条目必须是单个字母（不是标签）、任意相邻两项不得纵向重叠。

测量过程中验证器自身出过两次错，都已修并记录在 commit 里：
`auto.Control(AutomationId=...)` 走缓存条件，切页后返回**上一页**的 rail（27 项、
坐标看起来完全合理），从根遍历则一个都没有；以及把一个页签的状态带进下一个页签，
无法区分"陈旧元素"和"真缺陷"——现在五个状态各自从新启动的应用进入。

---

## 6. 字母排序「悬浮放大 + 波浪式渐变」 —— `partial`

**已实现，未能证明。** 这一项必须分开说，因为它正是本项目栽过的那类坑。

实现：`IndexRailLetter_PointerEntered` 把指针下的字母放大到 1.9x，邻居沿余弦衰减变暗，
第三个字母外归零。只写 `RenderTransform` 和 `Opacity`，**不碰 `Width`/`FontSize`**——
动布局会让字母栏每次悬浮都重新测量并把邻居推开，那正是报告里"按钮乱跳"的症状。

第一版监听在 `ItemsRepeater` 自己的 `PointerMoved` 上，从不触发：无 `Background` 的
`ItemsRepeater` 在自身区域不可命中，只有子元素可命中。改为每个字母自己的
`PointerEntered`。

**为什么不能证明**，三次尝试依次失败：

1. **UIA 包围盒**前后比较：growth=0。原因是 `RenderTransform` 在渲染期应用，而 UIA 报告
   的是**布局**矩形——放大到 1.9x 的字母仍报 32 px。测量工具对此类改动天生失明。
2. **整栏墨迹行数**：912 / 912。饱和——27 个字母本来就覆盖整列。
3. **局部墨迹像素数**：1757 / 1757。

第 3 个结果加了日志才定案：`PointerEntered` **在该环境下根本不会触发**。本仓库所有 MCP
探针都在 `background_window.foreground_guard()` 下运行，断言探针绝不抢前台——而非前台
窗口收不到 hover 输入。`SetCursorPos` 移动的是一个系统并未路由指针输入的窗口上的光标。

所以 `probe_rail_hover_wave.py` 是**探针，不打印 VERDICT**。它的 docstring 记了四种日后
可定案的办法及各自代价，包括那条不舒服的：若这个效果值得回归测试，就应该让波浪同时写一个
UIA 可见属性——即为了测试而改产品。

```bash
python tools/mvpw-mcp/probe_rail_hover_wave.py   # 记录实测，不下结论
```

---

## 7. 视频库「大路径 + 分栏」 —— `done`（分栏 `open`）

路径部分已完成。修复前名字和完整路径在**同一行**，名字 BodyStrong（约 14 px）、
路径 Caption（约 12 px），读起来像一串连着跑的标签，而路径——唯一能区分两个都叫
"Downloads" 的文件夹的东西——是表头里最不显眼的元素。现在路径独占第二行、跨满剩余列、
Subtitle 字号：

| | 高度 | 位置 |
|---|---|---|
| 文件夹名 `Hades` | 33 px | y=510 |
| **路径** | **47 px** | **y=548（独立一行）** |

分栏**未完成**：需求是"分栏文件夹图片样式，而非把视频全部展开"。当前文件夹视图已有
分组表头 + 网格，但没有文件夹**封面图**。这一项仍是 `open`。

---

## 8. 视频库不需要右侧字母索引 —— `done`

实测修复前：`VideoTabFolders` 有 27 个字母按钮，`VideoTabAll` 没有。条件写成了

```csharp
var isGrouped = _librarySection == LibrarySectionMusic
    ? _musicGrouping != MusicGrouping.Songs
    : _videoFolderView;      // <- 视频库只要在文件夹视图就成立
```

而它自己的 XAML 注释早就写着"只给音乐库的分组页签；视频库按目录分组，没有首字母"。
现在条件与注释一致。视频库按**目录**分组，目录没有首字母可跳。

```bash
python tools/mvpw-mcp/verify_letter_rail.py   # VERDICT: PASS
```

---

## 9. 播放队列右上角「添加文件」 —— `done`（点击行为未证）

修复前队列只有「清除」x=892 和「添加到」x=1043，都贴左；右侧空无一物。每个库分区都在
右上角放自己的动作（音乐库「添加文件夹」x=2844）。

修复后：

| 按钮 | 位置 |
|---|---|
| 清除 | (892, 485) |
| 添加到 | (1043, 485) |
| **打开文件** | **(2890, 485)** |

```bash
python tools/mvpw-mcp/verify_queue_button.py   # VERDICT: PASS
```

验证器断言的不只是"存在"，而是：在窗口右边缘附近、与其他队列动作同一行、且在**它们的
右边**。「有个按钮在某处」不是报告要的东西。

`QueueOpenFile_Click` 直接调 `OpenFileFromShell()` 而不转调
`LibraryOpenFileButton_Click`：后者按当前分区分支，Music/Videos 走
`AddLibraryFoldersAsync`（选**文件夹**）。从队列走 else 分支今天恰好正确，但只差一个
分区判断就会悄悄变成文件夹选择器。

**未证**：点击是否真的弹出文件选择器。`probe_open_file_picker.py` 记录实测——处理器确实
执行（有日志），但没有新窗口、没有新进程、没有异常。这不构成对按钮的指控：

* 它调用的 `OpenFileFromShell()` → `FileOpenPicker(_appWindow.Id)` 与 shell 菜单走的是
  **同一个**调用。
* 本机没有**任何**其他打开文件按钮可作对照：Home 空状态因已绑定文件夹而不渲染，
  视频库给的是「添加文件夹」。
* WinAppSDK 的 picker 走独立进程代理，可能因为机器属性（无交互式 shell 会话等）失败，
  而不会把错误抛回应用。

```bash
python tools/mvpw-mcp/probe_open_file_picker.py   # 记录实测，不下结论
```

探针 docstring 列了三个能定案的实验。

---

## 本轮确立的规则（写给下一个人）

1. **测量"看起来对不对"要用能看见该变化的仪器。** `RenderTransform`、composition 输出、
   UIA 布局矩形——这三样都让"值没变"成立而产品完全正常。三次误判都出在这。
2. **验证器自己也要能被证伪。** `auto.Control` 走缓存、`SetCursorPos` 送不进前台窗口、
   整栏墨迹饱和、正则永不匹配——每一个都曾给出"产品坏了"或"产品好了"的错误结论。
3. **不给不能判定的东西盖 VERDICT。** `probe_*` 前缀就是这个意思：记录实测，尽列后续
   实验，不冒充结果。
