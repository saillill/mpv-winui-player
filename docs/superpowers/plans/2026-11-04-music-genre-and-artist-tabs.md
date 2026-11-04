# 音乐库艺术家页签与流派筛选 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把音乐库的「艺术家」页签从"按艺术家分组的曲目卡"改成"一人一块圆形人像瓦片 → 该艺术家的专辑 → 该专辑的曲目"，并给歌曲与专辑两个页签加上流派筛选。

**Architecture:** 艺术家瓦片复用 `LibraryCard` 控件新增的 `IsArtist` 模式（圆形裁剪 + 恒显示人形 + 关掉播放/更多/勾选/now-playing），几何仍从 `CardMetrics` 出。艺术家页复用现有 `AlbumHost` 覆盖层，在其内部多一个"艺术家态"，左上返回按钮共用。流派筛选落成 `Domains/Playback/Cards/MusicGenreFilter.cs` 里的纯函数（`,` `;` 双分隔、`OrdinalIgnoreCase`、命中专辑键），页面只做取值与重建。

**Tech Stack:** WinUI 3 / WindowsAppSDK 2.5.1（WinUI 子包 2.3.9）、net10.0-windows10.0.26100.0、C#、NUnit 4（`tests/conf`，plain net10.0，**不能**引用 Microsoft.UI.Xaml）、Python（检查器）。

**Spec:** `docs/superpowers/specs/2026-11-04-music-genre-and-artist-tabs-design.md`

## Global Constraints

以下逐条来自 spec，每个任务隐含包含：

- **不改 `CardMetrics` 的任何既有常量。** 本轮只新增方法，不新增、不调整常量。
- **卡片几何只有一处真源。** 新布局复用 `AlbumCardLayout()` / `CardMetrics.CellWidthFor` / `CellHeightFor`，不写第二份数字。
- **`tests/conf` 里不能出现 `Microsoft.UI.Xaml`。** 可单测逻辑必须是不引用它的纯类，且放在 `Domains/Playback/Cards/**`（csproj 已 glob）。
- **`UpdateSelectionBoxForState` 在 `SelectionBox.Visibility = inMode || _pointerInside` 之前不得出现 `return`，也不得出现 `if (_isAlbum)`。** `CardMetricsTests.EveryCardGetsATickBoxIncludingAlbums` 守着这两条。
- **改 XAML 前先跑 `python tools\check-xaml.py`。** XAML 注释里出现 `--` 报 WMC9997。
- **图标**：`FontFamily` 只写 `MpvIconFontFamily` 或 `SymbolThemeFontFamily`；`FontSize` 字面量不得等于 12 / 14 / 16 / 20（已令牌化）。
- **`FluentIcons.Gen.cs` 本轮不动**（生成器要联网拉 SVG，本机不通）。人形图标走捆绑字体的码位 `&#xF5BE;`（`ic_fluent_person_24_regular`，已在 `Assets/FluentSystemIcons-Regular.ttf` 实测存在）。
- **新增 AppLang 属性必须同时进九份 `Languages/*.json`。** 除 zh-CN / en-US 外，其余七份按既有做法填中文占位或英文占位（照该文件相邻键的现状）。
- **`tools/*-baseline.txt` 手工重锚**，绝不 `--update-baseline`。
- **构建用全名参数**：`.\build.ps1 -Configuration Release -Platform x64`。退出码 1 是沙箱噪声，判据是「0 个警告 0 个错误」且 `src/mpvw-app/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/mpvw.dll` 存在。
- **部署目录必须在工作区外**：`$env:MPVW_DEPLOY_DIR='C:\Apps\mpvw'`，判据 `differing: 0`。
- **只暂存仓库文件**，绝不 `bin/`、`obj/`、`dist/`、`libs/`。提交前 `grep -rn "\[DBG-"` 必须无输出。
- **`edit` 类工具会丢 BOM、会翻转换行。** 改完看 `git diff --check`，并确认 `--stat` 里没有整文件重写。

## Review Focus

spec 暗示、但没有任务的测试直接覆盖、最容易咬到真实使用者的五类：

1. **承载 entry 是已删除文件时的人像。** 瓦片背后那一首被删掉，圆里必须仍有人形图标——`ApplyCovers` 在无封面时按 `IsMissing` 藏起 fallback（`LibraryCard.xaml.cs:226`），艺术家瓦片要走自己的分支。
2. **同名专辑串曲目。** 从艺术家页点开专辑，若只按专辑名找，会列出别的艺术家同名专辑的曲目。
3. **回收串台。** 滚动让同一个 `LibraryCard` 实例从艺术家瓦片变成曲目卡（或反过来），圆角、人像、勾选框、播放键必须全部跟着新身份走。
4. **流派值里带分隔符与大小写。** `"Rock; Pop"`、`"rock, POP"`、`"  Rock  "`、只有空白的值。
5. **筛选后进专辑详情。** 筛选状态下列表里只剩命中曲目，点进专辑必须是整张，与卡片上的曲目数一致。

每条在下面都有对应任务与测试（1→Task 1、2→Task 4、3→Task 1/3、4→Task 5、5→Task 6）。

---

## 文件结构

**新建**

| 文件 | 职责 |
|---|---|
| `src/mpvw-app/Domains/Playback/Cards/MusicGenreFilter.cs` | 流派拆分、匹配、词表、命中专辑键。纯 BCL，无 WinUI。 |
| `tests/conf/MusicGenreFilterTests.cs` | 上面那个类的测试。 |
| `tests/conf/MusicGenreArtistTests.cs` | 本轮新增的两组纯函数与源码守卫断言。 |

**修改**

| 文件 | 改什么 |
|---|---|
| `Domains/Playback/Cards/CardMetrics.cs` | 新增 `CircleCornerRadius(double)`。不碰既有常量。 |
| `Domains/Playback/Controls/LibraryCard.xaml` | 新增人形 `FontIcon` 元素（在 `MediaBorder` 内，`FallbackIcon` 旁）。 |
| `Domains/Playback/Controls/LibraryCard.xaml.cs` | `IsArtist` 属性；圆形裁剪；人像恒显；标题取艺术家名；副标题收起；三个覆盖层与勾选框在 `IsArtist` 下不出现。 |
| `Domains/Playback/MpvPlayerPage.xaml` | 工具栏第三列 + 流派 `DropDownButton`；`AlbumHost` 内新增艺术家态（头 + 专辑网格）。 |
| `Domains/Playback/MpvPlayerPage.Library.Video.cs` | `_musicSort`；`UpdateVideoSortChrome` 按分区/页签取值；`VideoSort_Click` 写对字段；菜单项启用状态。 |
| `Domains/Playback/MpvPlayerPage.Library.Music.cs` | `BuildArtistTiles`；`OpenArtist` / `CloseArtist`；艺术家态的专辑网格与返回栈；`_openAlbum` 变 `(Album, Artist?)`。 |
| `Domains/Playback/MpvPlayerPage_Library.cs` | 工具栏显隐加页签维度；`ApplyLibraryFilter` 加流派一层与词表；空状态判据。 |
| `Domains/Playback/MpvPlayerPage.Library.Cards.cs` | `IsArtist` 赋值；艺术家点击分支。 |
| `Lexicon/Language/AppLang.Shell.cs` + `Languages/*.json` ×9 | 三个新键。 |

`LibraryEntry.cs` **不动**：艺术家身份挂在卡片上，不挂在 entry 上。

---

### Task 1: 卡片的 `IsArtist` 模式

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs`（末尾追加一个方法）
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml`（`MediaBorder` 的 `Grid` 内加一个 `FontIcon`）
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs`
- Test: `tests/conf/MusicGenreArtistTests.cs`（新建）

**Interfaces:**
- Produces:
  - `public static double CardMetrics.CircleCornerRadius(double diameter)` — 返回 `diameter / 2`。
  - `public bool LibraryCard.IsArtist { get; set; }` — 默认 `false`。设为 `true` 后该卡片画圆形瓦片、恒显示人形图标、无播放键/更多键/勾选框/now-playing 条、只有一行标题。
  - `public static bool LibraryCard.IsArtistSelected(...)` **不产生**——不要引入静态方法，身份就是实例属性。

- [ ] **Step 1: 写纯函数测试（先红）**

```csharp
// tests/conf/MusicGenreArtistTests.cs
[TestFixture]
public class MusicGenreArtistTests
{
    [Test]
    public void TheArtistTileIsACircle()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardMetrics.CircleCornerRadius(150), Is.EqualTo(75.0));
            Assert.That(CardMetrics.CircleCornerRadius(0), Is.EqualTo(0.0));
        });
    }
}
```

- [ ] **Step 2: 跑它，确认红**

Run: `dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t1`
Expected: 编译失败 `'CardMetrics' does not contain a definition for 'CircleCornerRadius'`。

- [ ] **Step 3: 在 `CardMetrics.cs` 里加方法**

```csharp
/// <summary>The corner radius that makes a square of this size a circle: half its side.</summary>
public static double CircleCornerRadius(double diameter) => diameter / 2;
```

放在 `CoverHeightFor` 之后。**不要新增常量**，不要改任何既有值。

- [ ] **Step 4: 跑测试，确认绿**

Run: `dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t1`
Expected: PASS，且既有 609 个测试仍全绿。

- [ ] **Step 5: 在人像元素写进 XAML**

在 `LibraryCard.xaml` 的 `MediaBorder` → `Grid` 里、`FallbackIcon` 的**后面**加：

```xml
<!--  艺术家瓦片的人像。恒显示，不取封面：一块瓦片代表一个人，标签里没有肖像可供取材。
      码位取自捆绑字体 FluentSystemIcons，ic_fluent_person_24_regular = U+F5BE（已用 fontTools
      在 Assets/FluentSystemIcons-Regular.ttf 里实测存在）。24 这个字面量不是已令牌化的尺寸
      （令牌是 12/14/16/20），check-icon-standard 允许它。 -->
<Viewbox
    x:Name="ArtistGlyph"
    Width="72"
    Height="72"
    HorizontalAlignment="Center"
    VerticalAlignment="Center"
    Visibility="Collapsed">
    <FontIcon
        FontFamily="{StaticResource MpvIconFontFamily}"
        FontSize="24"
        Foreground="{ThemeResource TextFillColorTertiaryBrush}"
        Glyph="&#xF5BE;" />
</Viewbox>
```

先跑 `python tools\check-xaml.py`，再继续。

- [ ] **Step 6: 在代码后置里实现 `IsArtist`**

`LibraryCard.xaml.cs` 新增：

```csharp
public bool IsArtist
{
    get => _isArtist;
    set
    {
        if (_isArtist == value) return;
        _isArtist = value;

        // 只重算几何与裁剪。封面、人像、标题、副标题**都不在这里重画**：页面按
        // `card.IsArtist = ...; card.IsAlbum = ...; card.SetEntry(...)` 的顺序赋值，
        // SetEntry 会用新身份把这几样全部画一遍；在这里再画一次会用一个卡片自己不可能
        // 知道的 cover 值（传 null）覆盖掉刚解出来的封面。
        //
        // 唯一的另一条路径是"同一张卡不换 entry 只换身份"，它不存在：艺术家瓦片与曲目卡
        // 从不同的 repeater 里来，换 repeater 必然是一次新的 SetEntry。
        ApplyCoverHeight();
    }
}
private bool _isArtist;
```

「新身份必须重画」这一条是 Review Focus 3 的落点：`IsArtist` 与 `SetEntry` 一样会改这张卡代表什么，两处都要重画。

`ApplyCoverHeight()` 末尾、`ApplyCoverClip()` 调用之前：

```csharp
var radius = _isArtist
    ? new CornerRadius(CardMetrics.CircleCornerRadius(height))
    : new CornerRadius(CardMetrics.CornerRadius);
MediaBorder.CornerRadius = radius;
```

`ApplyCoverClip()` 里把 `rounded.CornerRadius` 的两分量从 `(float)CardMetrics.CornerRadius` 改成：

```csharp
var r = (float)(_isArtist ? CardMetrics.CircleCornerRadius(width) : CardMetrics.CornerRadius);
```

**注意**：`width` 与 `height` 在方形瓦片下相等；用 `width` 算半径，不要混用两个。

- [ ] **Step 7: 让 `ApplyCovers` 在人像模式下不碰封面**

`ApplyCovers` 的第一句之前加分支（**不是**早退，是人像模式自己的完整路径）：

```csharp
if (_isArtist)
{
    CoverImage.Source = null;
    CoverImage.Visibility = Visibility.Collapsed;
    FallbackIcon.Visibility = Visibility.Collapsed;
    // 人像恒在，与 entry 是否 IsMissing 无关：瓦片代表一个人，不是那一首歌。
    ArtistGlyph.Visibility = Visibility.Visible;
    return;
}

ArtistGlyph.Visibility = Visibility.Collapsed;
```

这一条同时解决 Review Focus 1。

- [ ] **Step 8: `ApplyCaptions` 分支到艺术家名**

```csharp
if (_isArtist)
{
    TitleText.Text = string.IsNullOrWhiteSpace(entry.Artist)
        ? AppContext.AppLang.LibraryUnknownArtist
        : entry.Artist;

    // 一行，没有第二行：瓦片下只有名字。
    SubtitleText.Text = string.Empty;
    SubtitleText.Visibility = Visibility.Collapsed;
    return;
}
```

放在既有 `var fileName = FileNameOf(entry.Path);` 之前。

- [ ] **Step 9: 关掉三个覆盖层与勾选框**

`UpdateSelectionBoxForState`：**不要动** `SelectionBox.Visibility = inMode || _pointerInside` 那一行（它是守卫测试的锚点）。在它**之后**加：

```csharp
// 艺术家瓦片没有勾选框：勾一个人没有可操作的对象。
if (_isArtist)
{
    SelectionBox.Visibility = Visibility.Collapsed;
    SelectionBoxHover.Visibility = Visibility.Collapsed;
}
```

`UpdateFrameForState`：`IsArtist` 下 `HoverFrame.Visibility` 仍按 `_pointerInside || selected` 走，不改（悬停描边要保留）。

`ApplyCaptionEmphasis`：现在写 `_isArtist` 时 `NowPlayingBar.Visibility` 恒 `Collapsed`。

`ShowOverlay`：`_isArtist` 时把 `PlayButton`、`MoreButton`、`PlayButtonHover`、`MoreButtonHover` 全部 `Collapsed`（`HoverOverlay` 本身仍显示，因为它承载的是它们）。

- [ ] **Step 10: 加源码守卫测试**

在 `MusicGenreArtistTests.cs` 追加三条，路径常量照 `CardMetricsTests` 的写法（`CardCodePath`）：

```csharp
[Test] public void AnArtistTileDrawsNoDiscs()
[Test] public void AnArtistTileHidesTheTickBoxAfterTheAssignment()
[Test] public void TheArtistCaptionIsTheArtist()
```

断言：
1. `ShowOverlay` 方法体里出现 `if (_isArtist)`；
2. `UpdateSelectionBoxForState` 里，`SelectionBox.Visibility = inMode || _pointerInside` **之后**出现 `_isArtist`；
3. `ApplyCaptions` 里出现 `LibraryUnknownArtist`。

- [ ] **Step 11: 构建 + 测试 + 检查器**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t1
python tools\check-icon-standard.py; python tools\check-typography-standard.py
```

Expected：构建「0 个警告 0 个错误」；测试全绿；两个检查器退出码 0（若行号位移，按 spec 的手工重锚流程处理）。

- [ ] **Step 12: 提交**

```bash
git add src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs \
        src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml \
        src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs \
        tests/conf/MusicGenreArtistTests.cs \
        tools/icon-standard-baseline.txt tools/typography-baseline.txt
git commit -m "Cards: an artist tile is a circle with a person, and nothing to play"
```

---

### Task 2: 音乐库自己的排序状态，以及按页签决定工具栏

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Video.cs`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（`ApplyVideoChromeVisibility`）
- Test: `tests/conf/MusicGenreArtistTests.cs`（追加）

**Interfaces:**
- Consumes: 无。
- Produces:
  - `private VideoSortMode _musicSort = VideoSortMode.NameAsc;`（页面字段）
  - `private VideoSortMode EffectiveMusicSort` — 属性；在「专辑」页签上把 `Newest` / `Oldest` 收敛成 `NameAsc`，其余原样返回。
  - `private bool MusicSortHasTimeModes` — 属性；只有歌曲页签为 `true`。

- [ ] **Step 1: 写源码守卫测试（先红）**

```csharp
[Test]
public void TheSortMenuReadsAMusicFieldOnTheMusicTabs()
{
    var source = File.ReadAllText(PageVideoCodePath);
    Assert.That(source, Does.Contain("_musicSort"));
    Assert.That(source, Does.Contain("EffectiveMusicSort"));
}
```

- [ ] **Step 2: 跑它，确认红**

Run: `dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t2 --filter TheSortMenuReadsAMusicFieldOnTheMusicTabs`
Expected: FAIL（源码里没有 `_musicSort`）。

- [ ] **Step 3: 加字段与两个属性**

`MpvPlayerPage.Library.Video.cs`，紧跟 `_videoSort`：

```csharp
/// <summary>
/// 音乐库的排序。与视频库的 _videoSort 分开：共用一个字段会让改音乐顺序连带改视频库，
/// 也会让视频库选过的「最新」写在专辑页签上一个灰掉的选项上。
/// </summary>
private VideoSortMode _musicSort = VideoSortMode.NameAsc;

/// <summary>当前页签上真正生效的音乐排序。专辑页签没有时间维度，两种时间序收敛成名字序。</summary>
private VideoSortMode EffectiveMusicSort =>
    !MusicSortHasTimeModes && _musicSort is VideoSortMode.Newest or VideoSortMode.Oldest
        ? VideoSortMode.NameAsc
        : _musicSort;

/// <summary>只有歌曲页签有时间序；专辑页签排的是组，组没有时间。</summary>
private bool MusicSortHasTimeModes =>
    _librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Songs;
```

上式的形状是实现时确定的：写作合取而不是析取，因为它的两个读取点都在音乐分区内，合取在音乐分区内取值相同，而在音乐分区外诚实地说"没有时间序"而不是意外为真。

- [ ] **Step 4: `UpdateVideoSortChrome` 按分区/页签取值**

改成：`isMusic = _librarySection == LibrarySectionMusic`；`current = isMusic ? EffectiveMusicSort : _videoSort`；四个 `IsChecked` 与 `Content` 都读 `current`；再补：

```csharp
SortByNewest.IsEnabled = !(isMusic && !MusicSortHasTimeModes);
SortByOldest.IsEnabled = !(isMusic && !MusicSortHasTimeModes);
```

- [ ] **Step 5: `VideoSort_Click` 写对字段**

`_videoSort = ...` 改为：

```csharp
if (_librarySection == LibrarySectionMusic) _musicSort = parsed; else _videoSort = parsed;
```

- [ ] **Step 6: `ApplyLibraryFilter` 里音乐 Songs 分支接上排序**

`MpvPlayerPage_Library.cs:1274` 的 `_ => SortVideoEntries(cards, VideoSortMode.NameAsc)` 改成：

```csharp
_ => SortVideoEntries(cards, EffectiveMusicSort),
```

专辑/艺术家两个分支的组内排序保持按标签，但**专辑页签的组顺序**要跟随 `EffectiveMusicSort`：见 Task 6。

- [ ] **Step 7: 工具栏整行在艺术家页签隐藏**

`_videoChromeWanted` 在 `ApplySectionHeader` 里赋值（`MpvPlayerPage_Library.cs:338`）。改那一行的真值表达式，加页签条件：

```csharp
_videoChromeWanted = ((isVideos && hasVideoFolders) || (isMusic && hasMusicFolders))
    && !(isMusic && _musicGrouping == MusicGrouping.Artists);
```

**这是唯一的判据。** `VideoSortButton` 与（Task 5 的）`MusicGenreButton` 都在这行里，行收起时一起消失；两个按钮**不要各自再判一次页签**——两处真相同一个答案，正是本仓库反复踩的那个缺陷形状。

`_musicGrouping` 变化现在也要重跑这个判据，所以 `SetMusicGrouping`（`MpvPlayerPage.Library.Music.cs:55`）在 `ApplyLibraryFilter()` 之前补一句 `ApplySectionHeader();`——`ApplySectionHeader` 是写这个字段的地方，切页签必须经过它。

- [ ] **Step 8: 跑测试 + 构建**

Run: `dotnet test ... --filter TheSortMenuReadsAMusicFieldOnTheMusicTabs` → PASS
Run: `.\build.ps1 -Configuration Release -Platform x64` → 0 错误

- [ ] **Step 9: UIA 验证排序真的生效**

部署后切到音乐库「歌曲」页签，点排序菜单选 Z-A，用 `winapp ui search` 取前两张卡的标题，确认顺序反转；再切到视频库，确认视频顺序**未变**。

- [ ] **Step 10: 提交**

```bash
git commit -m "Library: the music library sorts itself, and the artists tab keeps one row"
```

---

### Task 3: 艺术家页签（头像瓦片 → 艺术家页 → 专辑详情）

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Music.cs`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml`（`AlbumHost` 内）
- Modify: `src/mpvw-app/Lexicon/Language/AppLang.Shell.cs`、`src/mpvw-app/Languages/*.json` ×9
- Test: `tests/conf/MusicGenreArtistTests.cs`（追加）

**Interfaces:**
- Consumes: `LibraryCard.IsArtist`（Task 1）、`CardMetrics.CircleCornerRadius`（Task 1）、`EffectiveMusicSort`（Task 2）。
- Produces:
  - `private void BuildArtistTiles(List<LibraryEntry> entries)` — 填 `VideoFolderGroups`。
  - `private void OpenArtist(string artist)` — `artist` 是**空串**表示「未知艺术家」桶。
  - `private void CloseArtist()` — 回到瓦片网格。
  - `private (string Album, string? Artist) _openAlbum` — 字段类型由 `string?` 改为元组；`Artist` 为 `null` 表示从专辑页签进来，不按艺术家收窄。
  - `private string? _openArtist` — `null` 表示宿主不在艺术家态。

- [ ] **Step 1: 写源码守卫测试（先红）**

```csharp
[Test]
public void TheArtistTabBuildsTilesNotTrackCards()
{
    var music = File.ReadAllText(PageMusicCodePath);   // MpvPlayerPage.Library.Music.cs
    var page = File.ReadAllText(PageLibraryCodePath);  // MpvPlayerPage_Library.cs

    Assert.That(music, Does.Contain("private void BuildArtistTiles"),
        "the artists tab has no tile builder");

    Assert.That(page, Does.Contain("MusicGrouping.Albums) BuildMusicTagGroups"),
        "BuildMusicTagGroups must serve only the Albums tab");
}

[Test]
public void AnAlbumOpenedFromAnArtistIsScopedToThatArtist()
{
    var music = File.ReadAllText(PageMusicCodePath);

    Assert.That(music, Does.Contain("private (string Album, string? Artist) _openAlbum"),
        "the open album must carry the artist, or two artists' same-named albums merge");
}
```

两个路径常量照 `CardMetricsTests` 的写法声明（`Path.Combine` 到仓库根下的 `src/mpvw-app/Domains/Playback/`）。

- [ ] **Step 2: 跑它，确认红**

- [ ] **Step 3: 加语言键**

`AppLang.Shell.cs`：

```csharp
public string LibraryBackToArtist { get; set; } = "Back to artist";
```

九份 `Languages/*.json` 各加同键：zh-CN「返回艺术家」，zh-TW「返回藝術家」，其余照相邻键的语言填（en-US 用上面的默认值；de/es/fr/ja/ko/ru 填该语言，无把握时填英文）。

跑 `python tools\check-localization.py` 确认绿。

- [ ] **Step 4: `BuildArtistTiles`**

签名与数据形状：

```csharp
private void BuildArtistTiles(List<LibraryEntry> entries)
```

- `entries` 已是音乐库、已过搜索；**流派在艺术家页签不参与**（spec 8.3.6），所以这里拿到的就是该艺术家全部曲目。
- 按 `Artist` 分组，键为 `string.IsNullOrWhiteSpace(e.Artist) ? "\uFFFF" : e.Artist`，比较器 `StringComparer.CurrentCultureIgnoreCase`；显示名 `group.Key == "\uFFFF" ? AppLang.LibraryUnknownArtist : group.Key`。
- 每组构造 `VideoFolderGroup(name, items: [该组第一首曲目], Path: "", Letter: LibraryIndexLetter.For(name, ch => PinyinInitial.For(ch.ToString())))`。
  **`Items` 必须只有一项**，只有一项才会渲染成一块瓦片。repeater 的 `ItemsSource` 就是 `group.Items`，它有几项就画几张卡；把该艺术家的全部曲目塞进去，一个艺术家会变成一墙曲目卡，正是这个页签要修掉的东西。该艺术家的完整曲目由 `OpenArtist` 重新从 `_libraryAll` 取，不靠这个字段。
- 排序与 `BuildMusicTagGroups` 一致：先 `Array.IndexOf(LibraryIndexLetter.All, group.Letter)`，再 `ThenBy(Name)`。
- 组头沿用现有 `MusicGroupHeaderTemplate`？**不**——那一版带 56px 封面和曲目数，艺术家瓦片不需要封面，也不该显示曲目数。新增 `DataTemplate x:Key="ArtistGroupHeaderTemplate"`：只有一个 `TextBlock`（组名字母或名字，`BodyStrongTextBlockStyle`，`Foreground` 用 `TextFillColorSecondaryBrush`，`Margin="0,16,0,8"`）。spec 第 12 节把组头疏密留给实现者，这是最小形态。
- 每组一个 `ItemsRepeater`，`ItemTemplate = Resources["LibraryCardControlTemplate"]`，`Layout = AlbumCardLayout()`，`ItemsSource = group.Items`，订阅 `ElementPrepared`。
- 组头 `ContentPresenter` 登记进 `_groupHeaders`（首组胜），最后 `BuildLibraryIndexRail(groups)`。
- **格子高度用现成的 `AlbumCardLayout()`**，它按两行说明算高；瓦片只有一行，底部会多出约一行文字的空白。这是刻意的：`CardMetrics` 本轮不动一个常量，而两页签列对齐比消掉这点空白更重要。若观感不可接受，在验证步骤里作为发现上报，不要在实现里改 `CardMetrics`。

- [ ] **Step 5: `ElementPrepared` 里标出艺术家瓦片**

`LibraryGrid_ElementPrepared` 的 `card.IsAlbum = ...` 之前加：

```csharp
// 艺术家瓦片：圆、人像、只有名字。身份挂在卡片上，不挂在 entry 上——同一批 entry
// 实例也喂给歌曲网格与主页，标在 entry 上会漏出去。
var isArtistTile = _librarySection == LibrarySectionMusic
    && _musicGrouping == MusicGrouping.Artists;

card.IsArtist = isArtistTile;
card.IsAlbum = isArtistTile || (!entry.IsVideo && MediaKinds.IsAudio(entry.Path));
```

`IsArtist` 必须在 `IsAlbum` 与 `SetEntry` 之前赋值（它决定裁剪与封面分支）。

- [ ] **Step 6: 点击分支**

`HandleCardActivation`：选择模式判断之后、Albums 分支之前：

```csharp
if (_librarySection == LibrarySectionMusic && _musicGrouping == MusicGrouping.Artists)
{
    OpenArtist(entry.Artist);   // 空串 = 未知艺术家桶
    return;
}
```

- [ ] **Step 7: `_openAlbum` 换成元组，并收窄**

字段：`private (string Album, string? Artist) _openAlbum;`，配套 `private bool _albumOpened;`（元组不能判空）。

`OpenAlbum` 改为两个入口：

```csharp
private void OpenAlbum(string album) => OpenAlbum(album, artist: null);
private void OpenAlbum(string album, string? artist)
```

筛选条件在既有 `string.Equals(album key, album, ...)` 之后追一条：

```csharp
.Where(e => artist is null || string.Equals(
    string.IsNullOrWhiteSpace(e.Artist) ? string.Empty : e.Artist,
    artist,
    StringComparison.CurrentCultureIgnoreCase))
```

**注意**：艺术家态下若同时存在艺术家页，`OpenAlbum(album, artist)` 要把 `_openArtist` 保留，并让返回按钮文案切到 `LibraryBackToArtist`。

`CloseAlbum`：先判 `_openArtist`——非 `null` 时回到艺术家态（`ApplyArtistChrome()`），否则按现有路径关宿主。

- [ ] **Step 8: `AlbumHost` 里加艺术家态**

`MpvPlayerPage.xaml` 的 `AlbumHost` 根是一个带两行 `RowDefinitions`（`Auto` / `*`）的 `Grid`，现有子元素是"头 StackPanel（Row 0）"和"曲目 ScrollViewer（Row 1）"。

在这个根 `Grid` 里加**第三个子元素**，`Grid.RowSpan="2"` 覆盖两行：

```xml
<!--  艺术家页。与专辑详情共用这一个宿主（spec 7.2）：两者互斥显示，左上返回按钮
      也共用，按当前态决定退到哪。RowSpan=2 是因为它没有专辑详情那种"头 + 列表"
      两段结构，整块归它。 -->
<Grid
    x:Name="ArtistHost"
    Grid.RowSpan="2"
    Background="{ThemeResource SolidBackgroundFillColorBaseBrush}"
    Visibility="Collapsed">
```

它内部装：

1. 左上返回按钮（`x:Name="ArtistBackButton"`，`Click="ArtistBack_Click"`），文案走 `LibraryAlbumBack`；
2. 200×200 圆形容器（`Border`，`CornerRadius="100"`，背景 `MpvMusicArtworkFallbackBrush`）内一个人形 `FontIcon`（`FontFamily="{StaticResource MpvIconFontFamily}"`、`Glyph="&#xF5BE;"`、`FontSize="64"`）；
3. 艺术家名 `TextBlock x:Name="ArtistDetailName"`（`FontSize="30"`、`FontWeight="SemiBold"`）；
4. `ItemsRepeater x:Name="ArtistAlbumGrid"`，`Layout` 在代码里设为 `AlbumCardLayout()`。

**没有摘要行，没有动作按钮**（spec 8.2）。

`ArtistAlbumGrid` 的 `ElementPrepared` 由页面处理：`card.IsArtist = false; card.IsAlbum = true; card.SetEntry(albumCarrier, ResolveCover(albumCarrier));`，其中 `albumCarrier` 是**该专辑的第一首曲目**（与瓦片同一套做法），点击走 `OpenAlbum(album, _openArtist)`。

- [ ] **Step 9: `OpenArtist` / `CloseArtist` / `ApplyArtistChrome`**

```csharp
private void OpenArtist(string artist)
private void CloseArtist()
private void ApplyArtistChrome()   // 文案、专辑网格、返回按钮的可访问名
```

`OpenArtist`：`_openArtist = artist`；从 `_libraryAll` 取该艺术家的曲目；按 `Album` 键分组（空键 = 未知专辑）；每组用一个承载 entry 组成网格；`AlbumHost.Visibility = Visible`、`LibraryHost` 收、`MotionBehaviors.FadeIn(AlbumHost)`；`ArtistHost.Visibility = Visible`、专辑详情那一层 `Collapsed`。

`CloseArtist`：`_openArtist = null`；`ArtistHost` 收；调 `HideSurfacesOverLibrary()`（它会收 `AlbumHost`）；`ApplyLibraryFilter()` 重建瓦片网格。

`HideSurfacesOverLibrary`（`MpvPlayerPage_Library.cs:1709`）补两行：`_openAlbum = default; _albumOpened = false; _openArtist = null;`，并收起 `ArtistHost`。

- [ ] **Step 10: 无障碍**

`ApplyArtistChrome` 里 `AutomationProperties.SetName(ArtistBackButton, lang.LibraryAlbumBack)` + `ToolTipService.SetToolTip(...)`；艺术家名 `TextBlock` 用 `AutomationProperties.LabeledBy` 指到自己不需要（它是文本，不是按钮）。

跑 `python tools\check-ui-tooltips.py`。

- [ ] **Step 11: `ApplyLibraryFilter` 分派**

`musicGrouped` 现在要区分两种：

```csharp
var musicGrouped = _librarySection == LibrarySectionMusic && _musicGrouping != MusicGrouping.Songs;
```

保持不变，但 `if (musicGrouped)` 分支改成：

```csharp
if (musicGrouped)
{
    if (_musicGrouping == MusicGrouping.Albums) BuildMusicTagGroups(cards);
    else BuildArtistTiles(cards);
}
```

- [ ] **Step 12: 构建 + 测试 + 检查器 + 部署**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t3
python tools\check-localization.py; python tools\check-name-collisions.py
$env:MPVW_DEPLOY_DIR='C:\Apps\mpvw'; pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

- [ ] **Step 13: UIA + 截图验证（存 `.superpowers\sdd\2026-11-04-genre-artist\evidence\`）**

1. 切「艺术家」页签 → 圆形人像瓦片，一人一块，名字在圆下，导轨可见；
2. 截图确认圆是圆、人像是人像、瓦片上没有播放键/更多键/勾选框；
3. 点一块瓦片 → 艺术家页（圆形人像 + 名字 + 专辑卡），返回按钮在左上；
4. 点一张专辑卡 → 曲目表；返回 → 回到艺术家页；再返回 → 回到瓦片网格；
5. 艺术家页签上工具栏整行不可见。

- [ ] **Step 14: 提交**

```bash
git commit -m "Library: the artists tab is people, and a person opens their albums"
```

---

### Task 4: `MusicGenreFilter` 纯类

**Files:**
- Create: `src/mpvw-app/Domains/Playback/Cards/MusicGenreFilter.cs`
- Test: `tests/conf/MusicGenreFilterTests.cs`（新建）

**Interfaces:**
- Produces（页面在 Task 5/6 消费，签名逐字）：

```csharp
internal enum MusicGenreKind { All, Unknown, Named }

internal readonly record struct MusicGenreSelection(MusicGenreKind Kind, string Name = "")
{
    public static MusicGenreSelection All { get; } = new(MusicGenreKind.All);
    public static MusicGenreSelection Unknown { get; } = new(MusicGenreKind.Unknown);
    public static MusicGenreSelection Named(string name) => new(MusicGenreKind.Named, name);
}

internal static class MusicGenreFilter
{
    internal static IReadOnlyList<string> Split(string? raw);
    internal static bool Matches(string? raw, MusicGenreSelection selection);
    internal static IReadOnlyList<string> Vocabulary(IEnumerable<(string Album, string Genre)> tracks);
    internal static IReadOnlyList<string> KeptAlbumKeys(
        IEnumerable<(string Album, string Genre)> tracks, MusicGenreSelection selection);
}
```

- [ ] **Step 1: 写全部测试（先红）**

```csharp
[TestFixture]
public class MusicGenreFilterTests
{
    [Test] public void OneValueStaysOneValue()
    [Test] public void CommasAndSemicolonsBothSplit()
    [Test] public void SurroundingSpaceIsDropped()
    [Test] public void CarelessCasingCollapsesToOneEntry()
    [Test] public void AnEmptyValueYieldsNoGenres()
    [Test] public void AllMatchesEveryFile()
    [Test] public void UnknownMatchesOnlyFilesWithNoGenre()
    [Test] public void ANamedGenreMatchesAnyOfTheFilesGenres()
    [Test] public void MatchingIgnoresCase()
    [Test] public void TheVocabularyIsSortedAndDeduped()
    [Test] public void KeptAlbumsIncludeEveryTrackOfAMatchingAlbum()
    [Test] public void AFilesWithNoAlbumUsesTheEmptyKey()
}
```

关键断言（照抄进测试；`Split` 的顺序是**原序**，`Vocabulary` 才是排序的）：

| 测试 | 断言 |
|---|---|
| OneValueStaysOneValue | `Split("Rock")` → `["Rock"]` |
| CommasAndSemicolonsBothSplit | `Split("Rock, Pop; Jazz")` → `["Rock","Pop","Jazz"]` |
| SurroundingSpaceIsDropped | `Split("  Rock , Pop  ")` → `["Rock","Pop"]` |
| CarelessCasingCollapsesToOneEntry | `Split("rock, POP, Rock")` 长度 2 |
| AnEmptyValueYieldsNoGenres | `Split(null)`、`Split("")`、`Split(" , ; ")` 都 `Is.Empty` |
| AllMatchesEveryFile | `Matches("", All)` 与 `Matches("Rock", All)` 都 `true` |
| UnknownMatchesOnlyFilesWithNoGenre | `Matches("", Unknown)` true；`Matches(" , ", Unknown)` true；`Matches("Rock", Unknown)` false |
| ANamedGenreMatchesAnyOfTheFilesGenres | `Matches("Rock, Pop", Named("Pop"))` true；`Matches("Rock, Pop", Named("Jazz"))` false |
| MatchingIgnoresCase | `Matches("rock", Named("ROCK"))` true |
| TheVocabularyIsSortedAndDeduped | 输入 `[("A","rock"),("B","Pop"),("C","Rock"),("D","")]` → `["Pop","rock"]`（区域性排序，大小写不敏感去重，**空值不进词表**） |
| KeptAlbumsIncludeEveryTrackOfAMatchingAlbum | 输入 `[("X","Rock"),("X","Pop"),("Y","Jazz"),("Z","")]`，`Named("Rock")` → `["X"]`；`Unknown` → `["Z"]`；`All` → 三个键都在 |
| AFilesWithNoAlbumUsesTheEmptyKey | 输入 `[("","Rock")]`，`Named("Rock")` → `[""]` |

- [ ] **Step 2: 跑它，确认红**

Run: `dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t4 --filter MusicGenreFilterTests`
Expected: 编译失败，`MusicGenreFilter` 不存在。

- [ ] **Step 3: 实现**

要点（实现者可自行选择循环写法）：
- `Split`：`raw?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)`，再去重（`OrdinalIgnoreCase`，首次出现胜出，保原序）。
- `Matches`：`All` → true；`Unknown` → `Split(raw).Count == 0`；`Named` → `Split(raw).Any(g => string.Equals(g, selection.Name, OrdinalIgnoreCase))`。
- `Vocabulary`：所有非空流派名，`Distinct(OrdinalIgnoreCase)`，`OrderBy(StringComparer.CurrentCultureIgnoreCase)`。
- `KeptAlbumKeys`：`tracks.Where(t => Matches(t.Genre, selection)).Select(t => t.Album ?? "").Distinct(OrdinalIgnoreCase)`，**保原序**（调用方要用它做"整张留下"的过滤集合）。

文件头注释写明两条约束：不引用 `Microsoft.UI.Xaml`（测试工程链接不了）与够不到 `AppContext`（空专辑键原样返回，页面负责映射成「未知专辑」）。

- [ ] **Step 4: 跑测试，确认绿**（含既有 609 条）

- [ ] **Step 5: 提交**

```bash
git commit -m "Library: genre tags split on both separators, and an album keeps its tracks"
```

---

### Task 5: 流派按钮与歌曲页签的筛选

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml`（工具栏）
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Video.cs`（`ApplyVideoStrings`）
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（`ApplyLibraryFilter`、`ApplyEmptyStateContent`）
- Modify: `src/mpvw-app/Lexicon/Language/AppLang.Shell.cs`、`Languages/*.json` ×9
- Test: `tests/conf/MusicGenreArtistTests.cs`（追加源码守卫）

**Interfaces:**
- Consumes: `MusicGenreSelection` / `MusicGenreFilter.*`（Task 4）。
- Produces:
  - `private MusicGenreSelection _musicGenre = MusicGenreSelection.All;`
  - `private IReadOnlyList<string> _musicGenreVocabulary = [];`
  - `private void ApplyGenreStrings()` — 文案、可访问名、tooltip、菜单项。
  - `private void MusicGenre_Click(object sender, RoutedEventArgs e)` — 读 `Tag`。

- [ ] **Step 1: 语言键**

`AppLang.Shell.cs`：

```csharp
public string LibraryGenreAll { get; set; } = "All genres";
public string LibraryUnknownGenre { get; set; } = "Unknown genre";
```

九份 json 同步（zh-CN：所有流派 / 未知流派）。跑 `python tools\check-localization.py`。

- [ ] **Step 2: XAML 加第三列**

工具栏 `Grid`：`ColumnDefinitions` 改成 `*` / `Auto` / `Auto`，`ColumnSpacing="{StaticResource MpvSpaceS}"`（原本没有间距，第二列是 `Auto` 靠右；加间距后第一列仍 `*` 撑着，`HorizontalAlignment="Right"` 保留）。

排序按钮（`VideoSortButton`）留在 `Grid.Column="1"`。新按钮：

```xml
<!--  流派筛选。位置按用户截图：排序按钮之右。文案显示当前值，与排序按钮同款：
      透明底、无边框、向下弹出。菜单项在 ApplyGenreStrings 里每次重建，因为流派集合
      来自当前曲目，不缓存。 -->
<DropDownButton
    x:Name="MusicGenreButton"
    Grid.Column="2"
    Padding="8,4"
    HorizontalAlignment="Right"
    Background="Transparent"
    BorderThickness="0"
    Visibility="Collapsed">
    <DropDownButton.Flyout>
        <MenuFlyout Placement="Bottom" />
    </DropDownButton.Flyout>
</DropDownButton>
```

先跑 `python tools\check-xaml.py`。菜单项全部由代码填，不在这里写死。

- [ ] **Step 3: 字段，以及按钮跟着行走**

`MpvPlayerPage_Library.cs` 加两个字段（见 Interfaces）。

**`MusicGenreButton` 的显隐不自己判页签。** 它和 `VideoSortButton` 都在 `VideoChrome` 这一行里，Task 2 Step 7 已经让整行在艺术家页签收起，行收起时两个按钮一起消失。

要做的只有一件事：这行**原本只给视频库和音乐库出现**，现在音乐库多了一个流派按钮，所以它在音乐库出现时这个按钮必须可见。给 XAML 里的 `MusicGenreButton` 一个 `Visibility="Visible"`（替换 Task 5 Step 2 里写的 `Collapsed`）：

```xml
Visibility="Visible"
```

**不要**在 `ApplyVideoChromeVisibility` 里再写一次 `_musicGrouping != Artists`——那是第二份真相，两份迟早会不一致（这正是本仓库「两样东西约定契约、没有东西比较」的缺陷形状，`AGENTS.md` 有一整节讲它）。

`ApplyVideoChromeVisibility` 现在的确有 `isMusic` 局部变量用于切换 `VideoPlayAllButton` / `MusicShuffleAllButton`，**那个保持原样**，不要动。

- [ ] **Step 4: `ApplyGenreStrings`**

- `AutomationProperties.SetName(MusicGenreButton, lang.LibraryColumnGenre)` + `ToolTipService.SetToolTip(...)`；
- 标题文案：`MusicGenreButton.Content = $"{lang.LibraryColumnGenre}: {CurrentGenreLabel()}"`，其中 `CurrentGenreLabel()`：`All` → `lang.LibraryGenreAll`；`Unknown` → `lang.LibraryUnknownGenre`；`Named` → 名字原样；
- 重建菜单：清空 `((MenuFlyout)MusicGenreButton.Flyout).Items`，依次加
  1. `RadioMenuFlyoutItem` `Text = lang.LibraryGenreAll`、`GroupName = "MusicGenre"`、`Tag = "all"`、`IsChecked = _musicGenre.Kind == All`；
  2. 每个 `_musicGenreVocabulary` 名字一项，`Tag = "genre:" + name`，`IsChecked = _musicGenre.Kind == Named && 同名忽略大小写`；
  3. `RadioMenuFlyoutItem` `Text = lang.LibraryUnknownGenre`、`Tag = "unknown"`、`IsChecked = _musicGenre.Kind == Unknown`；
- 每项 `Click += MusicGenre_Click`。

在 `ApplyLibraryStrings` 的调用链里加一句 `ApplyGenreStrings()`（放在 `ApplyVideoStrings()` 之后）。

- [ ] **Step 5: 点击**

```csharp
private void MusicGenre_Click(object sender, RoutedEventArgs e)
{
    if (sender is not RadioMenuFlyoutItem { Tag: string tag }) return;

    _musicGenre = tag switch
    {
        "all" => MusicGenreSelection.All,
        "unknown" => MusicGenreSelection.Unknown,
        _ when tag.StartsWith("genre:", StringComparison.Ordinal)
            => MusicGenreSelection.Named(tag["genre:".Length..]),
        _ => MusicGenreSelection.All,
    };

    ApplyGenreStrings();

    if (_libraryLoaded) ApplyLibraryFilter();
}
```

- [ ] **Step 6: `ApplyLibraryFilter` 加流派一层**

顺序固定：分区 → 搜索 → **词表取自搜索之前** → 流派。

**先把"搜索之后、流派之前"那份列表提出来**，声明在 `ApplyLibraryFilter` 顶部（`var cards = entries.ToList();` 之前）：

```csharp
// 搜索之后、流派之前的音乐库快照。两处要它：流派的词表（下面），以及专辑页签的
// "整张保留"（Task 6）。声明在方法顶部而不是某个分支里，因为后者的分支条件与前者不同。
List<LibraryEntry>? beforeGenre = null;
```

然后在 `var cards = entries.ToList();` 之后：

```csharp
// 流派只作用于歌曲与专辑两个页签（spec 8.3.6）。艺术家页签不受影响，而且那个页签上
// 按钮是隐藏的——若不在这里排除，从歌曲页签带着一个流派切到艺术家页签，瓦片会莫名其妙
// 少一半，而屏幕上没有任何控件能解释为什么。
var genreApplies = _librarySection == LibrarySectionMusic
    && _musicGrouping != MusicGrouping.Artists;

if (genreApplies)
{
    // 词表取搜索之后、流派之前（spec 8.3.11）：取搜索结果会让选中的流派在换个
    // 搜索词后从菜单里消失，按钮仍写着它、菜单里却没有勾选。
    beforeGenre = cards;

    _musicGenreVocabulary = MusicGenreFilter.Vocabulary(
        beforeGenre.Select(e => (e.Album, e.Genre)));

    cards = beforeGenre.Where(e => MusicGenreFilter.Matches(e.Genre, _musicGenre)).ToList();
}
```

**注意**：这一段放在 `SortVideoEntries` / 音乐排序之前。词表只在音乐分区的歌曲与专辑页签刷新；其他页签与分区保留上一次的值（切回来时会被重算）。

- [ ] **Step 7: 空状态判据**

`ApplyEmptyStateContent`：

```csharp
var filtering = !string.IsNullOrWhiteSpace(_libraryFilter)
    || _musicGenre.Kind != MusicGenreKind.All;
```

`searching` 的既有用法全部换成 `filtering`。

- [ ] **Step 8: 源码守卫测试**

```csharp
[Test] public void TheGenreVocabularyIsTakenBeforeTheGenreFilter()
```

断言 `ApplyLibraryFilter` 里 `MusicGenreFilter.Vocabulary` 出现的位置**早于** `MusicGenreFilter.Matches`。

- [ ] **Step 9: 构建 + 测试 + 检查器 + 部署**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t5
python tools\check-localization.py; python tools\check-resources.py; python tools\check-scroll-maxwidth.py
python tools\check-ui-tooltips.py; python tools\check-icon-standard.py
$env:MPVW_DEPLOY_DIR='C:\Apps\mpvw'; pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

- [ ] **Step 10: UIA 验证**

1. 音乐库「歌曲」页签：按钮文案「流派：所有流派」，点开菜单顺序是 所有流派 → 字母序 → 未知流派（末尾）；
2. 选一个流派 → 只剩命中曲目；再输入一个搜索词 → 结果收窄（AND）；清空搜索 → 该流派仍生效；
3. 换个搜索词，打开菜单 → **已选流派仍在菜单里且勾选可见**；
4. 选「未知流派」→ 只剩无流派标签的曲目；
5. 筛出空结果 → 空状态显示「没有匹配的媒体」；
6. 切到艺术家页签 → 流派按钮不可见。

- [ ] **Step 11: 提交**

```bash
git commit -m "Library: a genre dropdown, and the songs tab obeys it"
```

---

### Task 6: 专辑页签的整张保留

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（`ApplyLibraryFilter` 的音乐分组分支）
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Music.cs`（`BuildMusicTagGroups` 的组顺序）
- Test: `tests/conf/MusicGenreFilterTests.cs`（追加一条综合用例）

**Interfaces:**
- Consumes: `MusicGenreFilter.KeptAlbumKeys`（Task 4）、`_musicGenre`（Task 5）、`EffectiveMusicSort`（Task 2）。
- Produces: 无新签名。行为变更：音乐「专辑」页签在流派筛选下按"命中专辑整张保留"裁剪，且组顺序跟随 `EffectiveMusicSort`。

- [ ] **Step 1: 追加综合测试**

```csharp
[Test]
public void AnAlbumIsKeptWholeWhenOneTrackMatches()
```

输入：`("X","Rock")`、`("X","Pop")`、`("X","")`、`("Y","Jazz")`。
断言：`KeptAlbumKeys(input, Named("Rock"))` → `["X"]`；再对同一份输入做页面侧那一步 `Where(album ∈ keptSet)`，结果里专辑 X 的三首**都在**，Y 不在。

- [ ] **Step 2: 跑它**

Run: `dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t6 --filter AnAlbumIsKeptWholeWhenOneTrackMatches`

若 Task 4 的 `KeptAlbumKeysIncludeEveryTrackOfAMatchingAlbum` 已让此用例直接通过（它大概率会），本条测试的作用是**把页面侧那一步裁剪也钉住**，不是红→绿循环。这一步的期望因此是 **PASS**；如果它红了，说明 Task 4 的 `KeptAlbumKeys` 行为与页面假设不一致，回到 Task 4 修。不要为了让它红而改实现。

- [ ] **Step 3: 页面里做裁剪**

`ApplyLibraryFilter` 的音乐分支，在 `cards` 已被流派过滤之后、排序之前：

```csharp
// 专辑页签：只要有一首命中就整张留下。命中的专辑键保留【当前结果集中】该专辑的
// 全部曲目，卡片与组头计数都按保留后的集合算——否则组头写 3 首、点进去是 12 行。
if (_librarySection == LibrarySectionMusic
    && _musicGrouping == MusicGrouping.Albums
    && _musicGenre.Kind != MusicGenreKind.All
    && beforeGenre is { } all)
{
    var kept = MusicGenreFilter.KeptAlbumKeys(
        all.Select(e => (e.Album, e.Genre)), _musicGenre);

    var keptSet = new HashSet<string>(kept, StringComparer.CurrentCultureIgnoreCase);

    cards = all
        .Where(e => keptSet.Contains(string.IsNullOrWhiteSpace(e.Album) ? "" : e.Album))
        .ToList();
}
```

`beforeGenre` 就是 Task 5 Step 6 声明并赋值的那一个（搜索之后、流派之前）。这一段放在流派过滤之后、排序之前。

- [ ] **Step 4: 组顺序跟随排序**

`BuildMusicTagGroups` 现在按 `Letter` 再按 `Name` 升序排。改成**整体倒序**——字母与组内名字一起反转，整列读作 Z→A：

```csharp
var ordered = groups
    .OrderBy(group => Array.IndexOf(LibraryIndexLetter.All, group.Letter))
    .ThenBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase);

groups = EffectiveMusicSort == VideoSortMode.NameDesc
    ? ordered.Reverse().ToList()
    : ordered.ToList();
```

**不要**只反转 `ThenBy` 的比较器：那会让字母仍是 A→Z、字母内部 Z→A，得到一个既不是 A-Z 也不是 Z-A 的顺序。导轨仍 A→Z（`BuildLibraryIndexRail` 不接排序参数），跳转按当前顺序找该字母的第一组——倒序时即最后一组。

- [ ] **Step 5: 构建 + 测试 + 全检查器**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-t6
$fail=0; foreach ($c in 'check-xaml','check-localization','check-ui-tooltips','check-icon-standard','check-typography-standard','check-name-collisions','check-resources','check-scroll-maxwidth','check-visual-state-layout','check-baseline-lines','check-doc-references','check-doc-freshness','check-doc-tooling') { python "tools\$c.py" 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { "FAIL: $c"; $fail++ } }; "失败检查器: $fail"
```

Expected：`失败检查器: 0`。

- [ ] **Step 6: UIA 验证**

1. 专辑页签选一个流派 → 只剩含该流派的专辑；组头计数 = 该专辑的**全部**曲目数；
2. 点进其中一张 → 曲目表是整张，行数与组头数字一致；
3. 专辑页签选 Z-A → 组顺序反转，导轨仍 A→Z 且跳得动；
4. 专辑页签上「最新 / 最早」两项置灰，按钮文案不写一个不生效的排序。

- [ ] **Step 7: 部署并确认干净树**

```powershell
$env:MPVW_DEPLOY_DIR='C:\Apps\mpvw'; pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
grep -rn "\[DBG-" src tests
git status --short
```

- [ ] **Step 8: 提交**

```bash
git commit -m "Library: a genre match keeps the whole album, and the album grid obeys the sort"
```

---

## 自检记录

**Spec 覆盖**：spec 8.1.1–8.1.8 → Task 1（2、3、4、6 的卡片侧）、Task 2（7、8）、Task 3（1、2、5、6）；8.2 全部 → Task 3；8.3.1–8.3.12 → Task 4（3、4、5）、Task 5（1、2、6、8、9、10、11、12）、Task 6（7）。无遗漏。

**类型一致性**：`MusicGenreSelection` / `MusicGenreKind` / `MusicGenreFilter` 的四个方法名在 Task 4 定义、Task 5/6 消费，逐字一致。`_openAlbum` 的元组形状只在 Task 3 定义与使用；`_openArtist` 同。`EffectiveMusicSort` / `MusicSortHasTimeModes` 在 Task 2 定义，Task 2、6 消费。

**Review Focus 落点**：1 → Task 1 Step 7 + Task 3 Step 14.2；2 → Task 3 Step 8 + Step 14.4；3 → Task 1 Step 6（`IsArtist` 与 `SetEntry` 都重画）+ Task 3 Step 5（赋值顺序）；4 → Task 4 的五个 `Split` 测试；5 → Task 6 Step 6.2。
