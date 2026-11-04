# 音乐库：艺术家页签重做 + 流派筛选 — 设计

写于 2026-11-04。分支 `feature/card-selection-and-sizes`。本文是开工前的设计，不含实现。

## 1. 这是什么活

音乐库有两个互相牵制的缺口：

1. **「艺术家」页签目前不是艺术家视图。** 它是「按艺术家分组」：每组一个 56px 组头，下面挂着该艺术家名下的**曲目卡**。用户要的是专辑墙那样的东西——一个艺术家一块瓦片，点进去看他的专辑，再点进去看这张专辑的歌。
2. **流派没有筛选。** `LibraryEntry.Genre` 已经读进来了，专辑详情页的曲目表已经有一列在显示它，但没有入口按它筛。

两件事在同一个页签上相遇：流派该不该影响艺术家页签。答案是"不影响"，但这个答案只有在艺术家页签先定形之后才写得出来——所以**先做艺术家，再做流派**，两个提交。

## 2. 目标

- 艺术家页签 = 一屏圆形人像瓦片，一人一块；保留字母导轨。
- 点瓦片 → 该艺术家的专辑列表；点专辑 → 该专辑的曲目表。返回逐层退。
- 音乐库工具栏多一个「流派：当前值」下拉，能与搜索叠加，能清空。
- 三个页签各自该有哪些工具，由页签决定，不再三个页签共用一套。

## 3. 非目标

- 不改专辑详情页已有的版面（200 封面、标题、三个动作按钮、曲目表）。
- 不改卡片几何。`CardMetrics` 本轮不动一个常量。
- 不新增播放/选择语义：艺术家瓦片不可勾选、不播放。
- 流派筛选不落盘，不进设置。
- 不动主页的两个 band、视频库、播放列表分区。

## 4. 反目标（做成了也算失败）

- 屏幕上出现一个点了没反应的下拉框或按钮。
- 艺术家页签里出现两种卡片形状（圆瓦片 + 曲目卡混排）。
- 流派筛选让同一个页签里的计数、组头、卡片三者互相对不上。
- 为了这件事在 `MpvPlayerPage.xaml` 里再抄一份网格尺寸。

## 5. 约束（仓库里的既成事实）

- **`CardMetrics` 是几何的唯一真源**，`tests/conf/CardMetricsTests.cs` 钉着它。新增布局必须复用它的方法，不许写第二份数字。
- **`tests/conf` 是 plain `net10.0`，不能引用 `Microsoft.UI.Xaml`。** 能被单测的逻辑必须放成纯类，且位于 `Domains/Playback/Cards/**`（csproj 已 glob 该目录）。
- **`check-xaml.py` 先跑**：XAML 注释里出现 `--` 报 WMC9997，会白烧一个构建周期。
- **`tools/*-baseline.txt` 按 `<path>:<line>:<kind>` 记账**。改 `MpvPlayerPage.xaml` 会让 `icon-standard-baseline.txt` 与 `typography-baseline.txt` 整体位移，必须手工重锚（绝不 `--update-baseline`）。
- **新增 AppLang 属性必须同时进九份 `Languages/*.json`**，否则 `check-localization.py` 红。
- **图标两条硬规则**：`FontFamily` 只能写 `MpvIconFontFamily` 或 `SymbolThemeFontFamily`；`FontSize` 字面量不许等于已令牌化的 12/14/16/20。
- **`FluentIcons.Gen.cs` 只能由 `tools/gen-fluent-icons.py` 生成**，而该脚本从 GitHub 拉 SVG。本机 `raw.githubusercontent.com` 当前 SSL 握手失败（已实测），所以**本轮不新增 PathIcon 图标常量**。
- 部署目录必须在工作区外（`MPVW_DEPLOY_DIR=C:\Apps\mpvw`），判据是 `differing: 0`。

## 6. 现状（我核对过的，带位置）

| 事实 | 位置 |
|---|---|
| 音乐库三个页签是 `MusicGrouping` 枚举 + 三个 RadioButton | `MpvPlayerPage_Library.cs:563`、`MpvPlayerPage.xaml:1073` |
| Albums / Artists 页签共用 `BuildMusicTagGroups`，一组 = 组头 + 一个 `ItemsRepeater`，**repeater 的 ItemsSource 是曲目** | `MpvPlayerPage.Library.Music.cs:344`、`:416` |
| 组头模板（56 封面 + 名字 + 计数） | `MpvPlayerPage.xaml:391` |
| 专辑详情宿主：整屏 `AlbumHost`，含左上返回、200 封面、标题、三动作、曲目 ListView | `MpvPlayerPage.xaml:1876` |
| 打开/关闭专辑：`OpenAlbum` / `CloseAlbum`，宿主显隐 + `MotionBehaviors.FadeIn` | `MpvPlayerPage.Library.Music.cs:156`、`:181` |
| 点卡片的语义：选择模式 → 勾选；Albums 页签 → 打开专辑；否则播放 | `MpvPlayerPage.Library.Cards.cs:480` |
| 工具栏一行两列：左 Play all / 随机播放，右排序 | `MpvPlayerPage.xaml:1139` |
| 排序在音乐 Songs 页签**是死代码**：写死 `NameAsc`，`_videoSort` 不参与 | `MpvPlayerPage_Library.cs:1274` |
| 随机播放取自 `_visibleEntries` | `MpvPlayerPage.Library.Video.cs:221` |
| 卡片 `IsAlbum` 只表示"方形瓦片"，由 entry 的媒体类型决定 | `MpvPlayerPage.Library.Cards.cs:743`、`LibraryCard.xaml.cs:719` |
| 圆角裁剪走合成层 `CompositionRoundedRectangleGeometry`（`FrameworkElement.Clip` 无圆角半径） | `LibraryCard.xaml.cs:501` |
| 字母导轨只给"音乐库的非 Songs 页签" | `MpvPlayerPage.Library.Cards.cs:111` |
| `Genre` 由 `MusicProperties.Genre` 读出，多值以 `", "` 拼成单串；读不到 = 空 | `LibraryTagsReader.cs:65`、`MusicTags.cs:18` |
| 空状态文案按"是否在搜索"分叉 | `MpvPlayerPage_Library.cs:417` |
| 关掉所有覆盖层的方法（专辑、音乐页） | `MpvPlayerPage_Library.cs:1709` |

## 7. 方案与取舍

### 7.1 艺术家页签的实现载体

| 方案 | 代价 |
|---|---|
| **A（选定）给 `LibraryCard` 加 `IsArtist` 模式** | 卡片新增一个模式：圆形裁剪 + 恒显示人形图标 + 关掉播放/更多/勾选/now-playing + 单行说明。几何仍从 `CardMetrics` 来。**模式是卡片上的属性，不是 entry 上的标记**——见 8.1.1 的理由。 |
| B 新建 `ArtistTile` 控件 | 职责更干净，但 6 DIP 留槽、标题带高度、格子尺寸会被抄第二份，而 `CardMetricsTests` 只守得住一处。 |
| C 沿用现有分组视图，只把组头画成人像 | 不改卡片，但"一屏瓦片"根本做不到——组头下面仍是曲目卡。 |

选 A：这条界面已经因为"两套卡片模板"付过一次代价（`LibraryCard` 被做成控件就是为了删掉两个 DataTemplate 和 `FindDescendant`）。

### 7.2 艺术家 → 专辑 的导航载体

| 方案 | 代价 |
|---|---|
| **A（选定）复用 `AlbumHost`，加一个"艺术家态"** | 宿主内部两种内容：专辑详情 / 艺术家专辑列表。左上返回按钮共用，按当前态决定退到哪。 |
| B 新增 `ArtistHost` 覆盖层 | 多一层宿主、多一处显隐与 `MotionBehaviors` 状态、`HideSurfacesOverLibrary` 多一行要记得。 |
| C 点艺术家 = 切到「专辑」页签 + 隐式过滤器 | 专辑页签现在没有返回按钮；且隐式过滤会在页签间跟着跑，看不出列表为什么少了。 |

选 A。用户的原话是「复用专辑页，哪有第三层，不需要新增退出按钮，退出按钮共用左上角返回按钮」。

### 7.3 流派筛选的落点

| 方案 | 代价 |
|---|---|
| **A（选定）工具栏右侧第三个下拉，动态菜单** | 菜单在每次打开时从当前 entries 重建；流派集合不缓存。 |
| B 把流派并进排序菜单 | 一个菜单混两种语义（筛选 vs 排序），且无法显示两个当前值。 |
| C 单独一行筛选条 | 挤走卡片区高度，且这一行已经有两个控件。 |

选 A，位置按用户截图：**排序按钮之右**。两个按钮都展示当前值（`排序方式：A-Z` / `流派：所有流派`）。

## 8. 已定决策

### 8.1 提交一：艺术家页签

1. **一艺术家一瓦片。** 键 = `entry.Artist`，空白归入单一「未知艺术家」桶（沿用现有分组规则）。
   - **承载 entry 与标记位置。** 一块瓦片背后是 N 首曲目，卡片仍要一个 `LibraryEntry` 才能渲染；用该艺术家名下的第一首做承载（艺术家页签没有排序，顺序就是字母分组内的既有顺序）。**「这是艺术家瓦片」这个事实标在卡片上（`LibraryCard.IsArtist`），不标在 entry 上。** 理由：`ApplyLibraryFilter` 把同一批 entry 实例同时发给歌曲网格、主页两栏和搜索结果（`MpvPlayerPage_Library.cs:1278`），标在 entry 上的标记会跟着漏到别的界面，把一首歌变成一块不能播放的圆瓦片。
   - **由此连带的三条**：承载 entry 为空或 `IsMissing` 时，圆里仍然是那只人形图标（卡片不得因 `IsArtist` 走"缺文件所以画空白"的分支）；`IsArtist` 时一律不取封面，`cover` 参数与 `entry.Thumbnail` 都要绕过而不是"不加载"——`LibraryCard.xaml.cs:226` 的 `IsMissing` 隐藏、`:210` 的封面优先都可能让圆圈空掉；艺术家瓦片列表**不进入缩略图解码的遍历源**，否则每块瓦片白烧一次解码（`MpvPlayerPage.Library.Cards.cs:293` 从 `_visibleEntries` 取）。
2. **圆形直径 150**，等于专辑卡内容宽，名字在圆下方**一行**。网格复用 `AlbumCardLayout()`，两页签的列因此对齐。
3. **圆内恒为人形图标**，不取封面。用捆绑字体里的 `ic_fluent_person_24_regular`（U+F5BE，已在 `FluentSystemIcons-Regular.ttf` 里实测存在），不走 `FluentIcons.Gen.cs`。
4. **瓦片没有播放键、没有更多键、没有勾选框、没有 now-playing 条。** 保留悬停描边与底色。
   - **勾选框必须在既有守卫测试下关掉。** `CardMetricsTests.EveryCardGetsATickBoxIncludingAlbums`（`tests/conf/CardMetricsTests.cs:416`）断言 `UpdateSelectionBoxForState` 里、赋值 `SelectionBox.Visibility = inMode` 之前**不得出现 `return`**。所以艺术家瓦片不走早退，走"条件并进表达式"。
5. **点瓦片 = 打开艺术家页**（见 8.2），不播放、不勾选。
6. **保留字母导轨，并按首字母分组。** 导轨沿用现有机制（跳到某个组头，`MpvPlayerPage.Library.Cards.cs:145`），所以瓦片墙**不是**一整片无分组网格，而是"字母标题 + 该字母下的瓦片网格"逐组排列，与专辑页签同形但组内是瓦片。首字母由 `LibraryIndexLetter.For(...)` + 拼音初值给出，与现有分组一致。
7. **排序按钮只在歌曲与专辑两个页签出现。** 歌曲页签四种排序全生效；专辑页签只有 A-Z / Z-A，另两项菜单项置灰；艺术家页签整行工具栏连同随机播放一起隐藏。
   - **音乐库有自己的排序字段**（`_musicSort`），不与视频库的 `_videoSort` 共用。共用会让改音乐库顺序连带改视频库，且视频库选过的「最新」会在专辑页签写在一个灰着的选项上——一个声明了却不生效的排序。`UpdateVideoSortChrome` 按分区读不同的值、点不同的单选标记。`SortVideoEntries` 的排序函数可复用，状态不复用。
8. **随机播放、流派按钮在艺术家页签隐藏。** 选中的流派值保留，回到另两个页签仍然生效。

### 8.2 艺术家页（复用 `AlbumHost`）

- **头**：200 圆形人像 + 艺术家名。**没有摘要行，没有动作按钮**（用户明确"都不要"）。
- **体**：该艺术家的专辑卡网格，复用专辑卡（150 宽、方形）。
- **专辑卡的说明行在这一层改成"专辑名 + 艺术家"**，不是文件名。理由：这一层列的是专辑，`"01 - Track.flac"` 在专辑墙上没有用。歌曲与专辑两个页签维持现状（普通文件名），不改——上一轮的「就是普通文件名，不要未知专辑/未知艺术家这种占位」仍然生效，本次只给艺术家页这一层另开一种说明。`AlbumTitle` / `AlbumArtistText` 两个属性本来就存在且全仓库无人绑定（`LibraryEntry.cs:143`、`:146`），这里正好是它们该被绑的地方。
- **专辑的判定**：该艺术家的曲目按 `Album` 键分组，空值归入「未知专辑」。一格 = 一个专辑键。
- **点专辑卡 → 切到专辑详情**（同一宿主），返回回到本页。
- **专辑详情按（艺术家, 专辑）筛选。** `OpenAlbum` 目前只用专辑名在 `_libraryAll` 里找（`MpvPlayerPage.Library.Music.cs:160`），两位艺术家各有一张同名专辑时会把两边的曲目混在一页——艺术家页一上线就会遇到。从艺术家页进入时带上艺术家作为第二条件（`_openAlbum` 由 `string` 变 `(string Album, string? Artist)`，`Artist` 为 null 表示"从专辑页签进来，不按艺术家收窄"）。`CloseAlbum` 与 `HideSurfacesOverLibrary` 跟着改。
- **返回**：艺术家页 → 关宿主回瓦片网格；专辑详情（由艺术家进入）→ 回艺术家页；专辑详情（由专辑页签进入）→ 关宿主。

### 8.3 提交二：流派筛选

1. **位置与形态**：工具栏第三列，`DropDownButton`，`Placement="Bottom"`，透明底无边框，与排序同款。文案 `流派：<当前值>`。
2. **菜单顺序**：`所有流派` → 各流派按当前区域性排序 → `未知流派`（**最后**，与分组头的未知桶一致）。
3. **未知流派 = 拆分后集合为空**，含"整份标签读不出来"的文件（`LibraryTagsReader` 把它们吞成 `MusicTags.Empty`，从 entry 层面无法与"没有流派标签"区分）。
4. **拆分规则**：原始值按 `,` 与 `;` 都拆，`Trim`，丢弃空项，`OrdinalIgnoreCase` 去重。`"Rock, Pop"` 与 `"Rock; Pop"` 得到同样两项，菜单里不会出现合成流派名。
5. **单选**，选中值是流派名字符串（`null` 表示「所有流派」）。匹配用 `OrdinalIgnoreCase` 对拆分后的成员。「未知流派」**不表示成一个可能和真实标签撞名的字符串**，用一个独立的值承载（哨兵枚举或独立的 bool），否则一个真写着 "unknown" 的文件会被当成未知桶。
6. **作用范围**：歌曲页签、专辑页签。**艺术家页签完全不受影响**（按钮在该页签也隐藏）。**从艺术家页打开的艺术家页同样不受影响**：它列该艺术家的全部专辑，不是"含该流派曲目的专辑"。主页、视频库、播放列表、队列不受影响。
7. **专辑页签：只要有一首命中就整张留下。** 命中的专辑键保留**当前结果集中**该专辑的全部曲目，卡片与组头计数都按保留后的集合算。
   - **专辑详情页不受流派过滤**：进去看到的是整张，与卡片上的曲目数一致。和搜索是同一套——搜索期间进专辑详情也是整张。
8. **与搜索是 AND。** 顺序固定：分区 → 搜索 → 流派。
9. **会话内状态**，重启回到「所有流派」，不进 `AppSettings`。
10. **按钮随时可用**，菜单在每次打开时重建。标签未读完时集合只有两项（所有 / 未知），标签到达后走既有的 `ApplyLibraryFilter` 重建路径，列表随之收敛。
11. **菜单的流派词表取"搜索之前"的音乐库全集**，不是当前搜索结果，也不是流派筛选之后的列表。取搜索结果会让选中的流派在换个搜索词后从菜单里消失，按钮仍写着它、菜单里却没有勾选、也看不出筛选还开着。实现上 `ApplyLibraryFilter` 在搜索那一步之前先算出词表并存在页面字段里，菜单每次打开时读它。
12. **空结果复用搜索的空状态**（「没有匹配的媒体 / 换个词试试」），不加新文案；`ApplyEmptyStateContent` 的判据从"在搜索"扩成"在筛选"。

## 9. 可单测的部分（放 `Domains/Playback/Cards/`，纯 BCL）

**约束：不能碰 `LibraryEntry`。** `LibraryEntry` 开头就是 `using Microsoft.UI.Xaml`（`LibraryEntry.cs:1`），而 `tests/conf` 是 plain `net10.0`，链接它直接 CS0234（`Cards/AlbumTrackCount.cs:18` 记着这次实测）。所以纯类只收值，不收 entry。

- `MusicGenreFilter.Split(string) → IReadOnlyList<string>`：`,` `;` 双分隔、Trim、去空、忽略大小写去重。
- `MusicGenreFilter.Matches(string rawGenre, MusicGenreSelection selection)`：`All` 恒真；`Unknown` 只匹配拆分后为空；`Genre("Rock")` 大小写不敏感命中，且 `"Rock, Pop"` 对 `"Rock"` 与 `"Pop"` 都真。
- `MusicGenreFilter.KeptAlbumKeys(IEnumerable<(string Album, string Genre)>, MusicGenreSelection) → IReadOnlyList<string>`：给定曲目投影，返回命中的专辑键（空专辑返回空键）。**键原样返回，不在这里翻译成「未知专辑」**——纯类够不到 `AppLang`，页面负责把空键映射成显示文案。

三条都必须是纯函数，且**每条都要先看它红过一次**（仓库规则：没见过红的检查器/测试，盲区未知）。

## 9.1 实现陷阱（构建者读代码时挑出的，逐条已验证）

1. **`IsArtist` 不能挂在 entry 上。** 同一批 entry 实例同时喂给歌曲网格、主页两栏和搜索（`MpvPlayerPage_Library.cs:1278`）；标在 entry 上的标记会漏到别的界面，把一首歌变成不能播放的圆瓦片。
2. **`IsMissing` 会让人像消失。** 承载 entry 恰好是已删除文件时，`LibraryCard.xaml.cs:226` 会把 fallback 图标藏起来，得到一个空圆。
3. **封面优先级会让人像消失。** `LibraryCard.xaml.cs:210` 是 `cover ?? entry.Thumbnail`，只在"不加载"层面绕过不够，要在渲染层面强制 `IsArtist` 恒走人形图标。
4. **瓦片承载的 entry 仍要进解码遍历，不要为它早退。** 初稿写的是"艺术家瓦片别进解码遍历，否则每块白烧一次解码"——这条是错的：承载 entry 是真实的曲目，用户在歌曲页签会用同一个实例显示封面；而 `LoadThumbnailsAsync`（`MpvPlayerPage.Library.Cards.cs:293`）每代库只跑一次，若恰好在艺术家页签加载库时早退，这一代就永远不会解码，切到歌曲页签是一片灰块。代价是艺术家页签停留期间为看不见的封面付一次解码，可接受。
5. **勾选框关法受守卫测试约束。** `CardMetricsTests.cs:416` 断言 `UpdateSelectionBoxForState` 在赋值前不得有 `return`，也禁止 `if (_isAlbum)` 复活。艺术家瓦片要把条件并进表达式，不能早退。
6. **字母导轨只有一种机制**：跳到某个组头（`MpvPlayerPage.Library.Cards.cs:145`），组头由分组构建时登记。所以瓦片墙必须带字母分组，否则导轨是死按钮。
7. **Z-A 与导轨的关系**：组倒序后导轨仍 A→Z，跳转按当前顺序找该字母的第一组（倒序时即最后一组）。
8. **排序状态分家**：`_videoSort` 共用会连带改视频库，并让专辑页签显示一个灰着的排序（`MpvPlayerPage.Library.Video.cs:46`、`:115`）。
9. **专辑详情按（艺术家, 专辑）收窄**：只按专辑名会在同名专辑上串曲目（`MpvPlayerPage.Library.Music.cs:160`）。
10. **专辑卡的说明行只在艺术家页那一层换成专辑名 + 艺术家**，其余页签保持文件名。`AlbumTitle` / `AlbumArtistText` 目前无人绑定（`LibraryEntry.cs:143`、`:146`）。

## 10. 影响面清单

| 文件 | 改动 |
|---|---|
| `Domains/Playback/MpvPlayerPage.xaml` | 工具栏加第三列与流派下拉；`AlbumHost` 头部加艺术家圆像、主体加专辑网格（或复用容器切换）；卡片模板不变 |
| `Domains/Playback/MpvPlayerPage.Library.Music.cs` | 艺术家页签改为瓦片网格；艺术家页打开/关闭；专辑态与艺术家态的切换 |
| `Domains/Playback/MpvPlayerPage_Library.cs` | 工具栏显隐加页签维度；`ApplyLibraryFilter` 加流派一层；空状态判据 |
| `Domains/Playback/MpvPlayerPage.Library.Video.cs` | 排序文案与可用性按页签分叉 |
| `Domains/Playback/MpvPlayerPage.Library.Cards.cs` | 瓦片的 `IsAlbum` / `IsArtist` 赋值；点击分支加艺术家；解码遍历跳过瓦片 |
| `Domains/Playback/Controls/LibraryCard.xaml(.cs)` | `IsArtist` 模式：圆形裁剪、恒显示人像、隐藏三组覆盖层、单行说明 |
| `Domains/Playback/Cards/MusicGenreFilter.cs` | **新增**，纯逻辑；只收值类型投影，不碰 `LibraryEntry` |
| `Lexicon/Language/AppLang*.cs` + `Languages/*.json` ×9 | 新键：`LibraryGenreAll`、`LibraryUnknownGenre`、`LibraryBackToArtist` |
| `tests/conf/` | 新增 `MusicGenreFilterTests.cs`（glob 已覆盖） |
| `tools/icon-standard-baseline.txt`、`tools/typography-baseline.txt` | 行号整体重锚 |

`LibraryEntry.cs` **不在影响面内**：艺术家标记落在卡片上，不落在 entry 上（见 9.1 第 1 条）。

## 11. 验证

每个提交都要走完：

```powershell
cd C:\Users\z2637\Documents\mpv-project\mpvw
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-<随机>
# 13 个检查器
$env:MPVW_DEPLOY_DIR='C:\Apps\mpvw'
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify   # differing: 0
```

行为验证（UIA + 截图，存 `.superpowers\sdd\2026-11-04-genre-artist\evidence\`）：

1. 艺术家页签显示圆形瓦片，一人一块，每个人像图标恒在（含无封面、含文件已删除的承载 entry）。
2. 点瓦片进艺术家页，只有专辑卡且说明行是专辑名 + 艺术家；点专辑进曲目表（且只含该艺术家的曲目）；返回逐层退。
3. 艺术家页签上没有排序、没有流派、没有随机播放。
4. 歌曲页签排序真的改变顺序（回归：曾经点了没反应）；改音乐库排序不影响视频库。
5. 专辑页签 Z-A 反转组顺序，"最新/最早"置灰且按钮文案不写一个不生效的排序。
6. 流派菜单顺序：所有 → 字母序 → 未知；换个搜索词后已选流派仍在菜单里且勾选可见。
7. 选流派后歌曲页签只剩命中曲目；专辑页签保留整张；两者组头计数一致；进专辑详情是整张。
8. 流派 + 搜索叠加；清空（选「所有流派」）后完整列表回来。
9. 空结果走搜索空状态。
10. 字母导轨在艺术家页签与分组后的专辑页签上都跳得动。

## 12. 留给实现者的判断

以下是我没有替实现者决定、且不会改变上面任何结论的细节：

- 艺术家页的专辑网格放在 `AlbumHost` 内部的哪个容器、用哪个 `ScrollViewer`，由实现者按现有 markup 的 padding 体系定。
- 流派菜单是每次 `Flyout.Opening` 重建还是点开前清空重建，两种都可，只要满足 8.3.11（词表来自搜索之前的全集）这一条。
- 返回按钮的两处文案切换放在 `ApplyAlbumChrome` 还是新方法里。
- 艺术家态的淡入是否复用 `MotionBehaviors.FadeIn`。
- 字母分组的具体表现：组头是「大写字母 + 该字母下人数」还是只有一个字母，由实现者按现有 `MusicGroupHeaderTemplate` 的疏密决定；导轨行为不变。
