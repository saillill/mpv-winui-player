# 卡片与音乐页重做：设计

2026-10-31。本文件是本轮工作的设计依据，**不是** `docs/` 下的契约。
`docs/` 只放「代码里当前的事实」，计划归这里。

---

## 一、意图与验收

用户的目标是 **B：重做设计**，验收是 **A：可指认的改前/改后对比**（沿用设置页的做法）。

本条线覆盖三块，用户已确认四件都要做：

1. 卡片悬停态：播放键位置与遮罩太重
2. 封面与文字的呼吸：留白、行距、层级
3. 两套卡片不一致，没有统一的尺度
4. 信息不够：缺时长、曲目数、正在播放指示

外加一件技术债（第 0 步，其余三块的**基础**）：

5. 卡片渲染靠 `FindDescendant(name)` 字符串查找，XAML 改名静默失败

顺序：**5 → 1/2/3 → 4 → 音乐库三页签 → 音乐播放页**。

## 二、本案推翻的既有前提

`docs/research/06-image-cards.md` 与 `Library.Cards.cs` 的三处注释都写：
「任意一条 `{x:Bind AlbumTitle}` 让编译器报 WMC9999」。

**这是错误归因。** 2026-10-31 实测（分支 `main`，`644a955`）：

| 试验 | 结果 |
|---|---|
| `{x:Bind AlbumArtistText}` | 编译通过 |
| `{x:Bind AlbumTitle}` | WMC9999 |
| `{x:Bind AlbumTitle}`，先把 `MpvPlayerPage.xaml:1920` 的 `x:Name="AlbumTitle"` 改名 | **编译通过**，且运行时渲染出 `i Love` |
| `{x:Bind Thumbnail}` 换掉 `SetCardCover` | 编译通过，**运行时封面不显示** |

真因：`MpvPlayerPage.xaml:1920` 有一个 `x:Name="AlbumTitle"` 的 TextBlock（专辑详情页标题），
它在 `MpvPlayerPage` 页面类上生成同名字段，与 `LibraryEntry.AlbumTitle` 撞名。
`AlbumArtistText` 没有这个冲突，所以一直能绑。

于是本案的**两条保留结论**：

- 文字标题**可以**走 `{x:Bind}`（旧注释说的崩溃不存在）
- 封面**不能**走 `{x:Bind}`（实测 `stddev 20.4 / 25 色` = 只剩回退图标；
  原代码同一张卡 `57.5 / 52258 色` = 真封面）

## 三、方案

### 已否掉的

| 方案 | 否掉的理由 |
|---|---|
| B：保留两个模板，只修文字绑定 | 281 行重复留着，「尺度不一致」这条治不好 |
| C：两个 `Control` + `DefaultStyleKey` | `GetTemplateChild` 仍按名字取，等于把同一个坑挪个位置；照 `CheckBoxExt`/`SliderExt` 先例会多一层模板开销 |

### 采用：A —— 一个带类型的卡片控件

`DataTemplate` 的根换成一个 `LibraryCard : UserControl`。封面、方形、标题、副标题、
选中态都是**类型化属性**，从 `ElementPrepared` 直接赋值。

**spike 已验证**（2026-10-31，三个问题一次跑完）：

| 问题 | 结果 |
|---|---|
| UserControl 能作 `ItemsRepeater` 模板根？ | 能。编译 40 XAML、0 error，卡片区渲染出控件内容 |
| `ElementPrepared` 交回的是控件本体？ | 是。`args.Element is LibraryCardTest` 直接成立 |
| 类型化属性到达屏幕？ | 到达。`typedCard.Caption = "TYPED-OK"` 在 UIA 树里读到 |

因此 `FindDescendant(card, "AlbumCardCoverImage")`、`SetCardCaption`、
`SetCardMediaSquare` 三处字符串/手工绘制全部删除。改名会变成**编译错误**，不再是静默空白。

### 卡片几何：为什么不能declarative

专辑卡的方形媒体区今天靠 `SetCardMediaSquare` 代码算。本案试过换成
`Height="{Binding ActualWidth, RelativeSource={RelativeSource Self}}"`（视频卡用的正是这条），
**实测失败**：媒体区塌成细条，封面位整块消失，截图里只剩一条 48px 的回退图标。
所以方形必须继续在控件内部按 `ActualWidth` 设——只是从「页面按名字找 Border」
变成「控件管自己的部件」。

## 四、已验证的边界（改之前要知道）

- **封面必须代码赋值**，不能绑定。见 §二。
- **方形必须代码设置**，不能声明式。见 §三。
- **文字标题可以绑定**，但控件化之后没有必要——属性赋值更直接，且能同时在
  语言切换时重发。
- 卡片分组视图（专辑/艺术家）的 repeater 由代码建（`BuildMusicTagGroups`），
  模板从 `Resources` 取；控件化不影响这条路径。
- `LibraryEntry` 已是 `INotifyPropertyChanged`，`Thumbnail` 变化会 raise；
  控件化后这个通知仍要用（封面晚到）。

## 五、目标（goals）

- 一个卡片控件，视频与专辑共用，靠一个宽高比参数区分（16:9 / 1:1）
- 悬停态：遮罩与两个按钮的几何集中在控件内；播放键**离开封面主体**
- 文字的呼吸：标题与副标题的行距、与封面的间距按一套令牌，不再散落字面量
- 两套卡片尺度统一：列距/行距/圆角/字号来自同一处
- 新增信息位：时长、曲目数、正在播放指示

## 六、非目标（non-goals）

- 不改 `ItemsRepeater` / `UniformGridLayout` 的选择（换 `ItemsView` 需要更新 WASDK，见
  `docs/research/02-music-page.md`，本轮不动）
- 不改 `WASDK 2.3.9` 的版本
- 不改音乐库的数据来源（`LibraryTagsReader` 的字段读取）
- 不动 `PlaylistPanel`（那是下一站）
- 不动侧栏（那是下一站）

## 七、反目标（anti-goals）

- 出现「看起来变了但断言量不出来」的改动——每一条视觉改动都要能写进 `ui-design-audit.py`
  的 board
- 为了统一而删掉有理由的差异（例如专辑卡两行标题 vs 视频卡两行，是有理由的）
- 用截图当唯一证据

## 八、已定的设计决定

用户 2026-10-31 逐条确认。

### 8.1 悬停态

**回到角上，遮罩改渐变。** 播放键左下、更多键右上；遮罩从整面
`SmokeFillColorDefaultBrush` 改成底部重、顶部透明的渐变。

这同时修掉一处注释与实现不符：`MpvPlayerPage.xaml:218-223` 写的是
"Play at the lower-left, the overflow at the lower-right ... so they do not sit on the
thumbnail's subject"，而代码里播放键是**正中心**、更多键在**右上**。正中心正好压在
专辑封面的正题上，是音乐卡最不该遮的位置。

### 8.2 正在播放

**封面色条 + 标题变色。** 封面底边一道 2px 主题色条，标题同时变主题色。

实现走**数据层标志位**：`LibraryEntry.IsNowPlaying`（`bool`，raise `PropertyChanged`）。
理由是滚动回收：卡片是复用模板，状态挂在元素上会跟着容器跑到另一个条目；
挂在 entry 上由通知驱动，回收后自然正确。

### 8.3 信息位

`LibraryEntry` 新增两个字段：

| 字段 | 类型 | 未知时 | 来源 |
|---|---|---|---|
| `TrackCount` | `int?` | `null`（不写 0 —— 0 是「这张专辑没有曲目」，是另一件事） | `LoadMusicTagsAsync` 读完后按 album 分组回填 |
| `IsNowPlaying` | `bool` | `false` | 播放路径设置 |

`TrackCount` 只有专辑页签的卡片显示；和封面一样有「晚到」时序，走同一套通知。

### 8.4 音乐库分组头：新写一个

现状是**借用视频库的文件夹头**：`MpvPlayerPage.Library.Music.cs:351`
`var headerTemplate = (DataTemplate)Resources["VideoFolderHeaderTemplate"]`。
那个头是为「目录」设计的 —— 64x36 封面 + BodyStrong 名字 + 一行 `Path`；
音乐分组的 `Path` 是 `string.Empty`，于是那行整个不渲染，留下空的大行高。

这是「三页签的分组呈现太维」的**结构原因，不是样式问题**。改为新写
`MusicGroupHeaderTemplate`：分组封面（取组内第一张专辑封面）、名称、曲目数。
视频库继续用文件夹头。

### 8.5 音乐库三页签 / 播放页：确认的问题

用户四选四全中：

1. 分组呈现太维、层级不清 → §8.4 的结构原因
2. 首字母导航与分组重复 → 右侧 A–Z 导轨与分组头同时存在，两套导航
3. 专辑详情页（封面 + 曲目表）
4. 音乐播放页的版面、歌词区

其中 3、4 是本轮**之后**的段（见 §十二 段的划分），它们的共同前提是
§三 的卡片控件落地。

### 8.6 一个实测到的副作用

`LoadMusicTagsAsync` 成功后调用 `ApplyLibraryFilter()`，后者重建整棵分组树。
效果：标签读完后滚动位置与分组头全部丢掉。

卡片控件化不修这个。正确的修法是把分组头做成可刷新的数据对象而不是重建整棵树，
和 §8.4 的新头是同一件事，所以并入 §8.4 的段里做。

## 九、验证方式

沿用设置页的 A 验收：

```powershell
python tools\ui-design-audit.py --list
python tools\ui-design-audit.py --board cards
python tools\ui-design-audit.py --board cards --snapshot
python tools\ui-design-audit.py --board cards --screenshot before.png
```

- board 是**数据表**，不是代码（`check-ui-tooltips.py` 的反面教材）
- 加 board 前先看它红：注入真实违规
- 像素统计用于封面临界判断：`stddev` 与 `distinct colours`，阈值对齐本案 §二 的实测值

## 十、决策登记

| # | 决策 | 来源 |
|---|---|---|
| 1 | B 目标、A 验收 | 用户（沿用） |
| 2 | 卡片控件化（方案 A） | 用户选，spike 已验证 |
| 3 | 技术债先行 | 用户确认四件都做 |
| 4 | 文字走属性、封面走属性、方形走代码 | 本案实测 |
| 5 | 不改 WASDK / 不换 ItemsView | 本案范围 |
| 6 | 悬停态：回角上 + 遮罩改渐变 | 用户选 |
| 7 | 正在播放：封面色条 + 标题变色 | 用户选 |
| 8 | 正在播放实现：`LibraryEntry.IsNowPlaying` 数据层标志位 | 用户选 |
| 9 | 音乐分组头：新写一个，不共用文件夹头 | 用户选 |
| 10 | `TrackCount` 用 `int?`，未知是 `null` 不是 0 | 本案提议，用户未反对 |
| 11 | `AlbumTitle` 撞名：改专辑详情页的字段名，不改 `LibraryEntry` | 本案实测 |
| 12 | 卡片几何令牌放 `Styles.xaml`，两个模板共用一处 | 本案提议（对应「尺度不一致」） |

## 十一、段的划分

本轮（卡片 + 音乐页）拆成四段，按依赖排序。每段独立可验证、独立可提交。

| 段 | 内容 | 为什么在这个位置 |
|---|---|---|
| **C1 卡片控件** | `LibraryCard : UserControl`；封/方/文字/选中全走类型化属性；删 `FindDescendant` 三处 | 后三段的共同基础。改完卡片外观不变，只有债务消失 |
| **C2 卡片视觉** | 悬停态回角上 + 渐变遮罩；标题/副标题行距与留白；两个模板并成一个（宽高比参数）；几何令牌 | 依赖 C1 的属性槽位 |
| **C3 信息位** | `TrackCount` / `IsNowPlaying` 字段 + 卡片上的色条与标题色 + 曲目数 | 依赖 C1 的属性槽位与 C2 的几何 |
| **C4 音乐分组头** | `MusicGroupHeaderTemplate`；分组封面 / 名称 / 曲目数；顺带修 §8.6 的重建副作用 | 依赖 C3 的曲目数才能显示它 |

**后续段（不在本轮）**：专辑详情页（§8.5-3）、音乐播放页与歌词区（§8.5-4）、
首字母导轨去重（§8.5-2，属于侧栏那一轮的导航话题）。

## 十二、遗留风险

1. **`AlbumTitle` 撞名会复发。** 改名 `MpvPlayerPage.xaml:1920` 的 `x:Name` 是修法，
   但下一次有人在页面里写 `x:Name="AlbumTitle"` 就又撞上。目前没有检查器看得见它
   （编译错误会响，所以危害有限；但错误信息是 WMC9999，不指向真正原因）。
   建议并入 C1：`check-xaml.py` 增加一条「`x:Name` 不得与 `LibraryEntry` 的公开属性同名」。
   加之前先按仓库规矩看它红。
2. **曲目数是整张专辑的事实，却挂在单条 entry 上。** 一张专辑 N 条曲目就有 N 份副本，
   改一次要写 N 次。数量小（一个专辑几十条）时不值得建索引；记录在此，量变大再改。
3. **`ApplyLibraryFilter` 的重建**（§8.6）在 C4 修；在那之前 C3 的正在播放色条要能
   在重建后重新出现 —— 因为 `IsNowPlaying` 在 entry 上、entry 会被 `FromHistory`
   重建，重建时若不搬运这个标志，色条会消失。C3 必须显式处理这一条。
