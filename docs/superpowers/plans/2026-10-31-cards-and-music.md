# 卡片与音乐库重做 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把卡片渲染从「按名字在视觉树里搜索」换成类型化属性，并在此之上重做卡片悬停态、信息层次与音乐库分组头。

**Architecture:** 一个 `LibraryCard : UserControl` 取代两个 `DataTemplate`。封面、方形高度、标题、副标题、选中态、正在播放都是控件上的类型化属性，由 `ElementPrepared` 直接赋值；不再有 `FindDescendant(name)`，也不再有任何数据绑定驱动卡片文字。卡片几何令牌抽到一个纯类 `CardMetrics`，由 `mpv-conf-test` 套件断言。

**Tech Stack:** WinUI 3 / WindowsAppSDK 2.5.1（WinUI 子包 2.3.9）、net10.0-windows10.0.26100.0、C#、NUnit 4（`tests/conf`）、Python（检查器与 `ui-design-audit.py`）。

**Spec:** `specs/2026-10-31-cards-and-music-redesign.md`

## Global Constraints

以下每一条来自 spec，逐字照抄，每个任务均隐含包含：

- **封面不能绑定。** 实测 `{x:Bind Thumbnail}` 不显示（`stddev 20.4 / 25 色`，只剩回退图标）。封面必须代码赋值。
- **方形高度不能声明式。** 实测自引用 `{Binding ActualWidth, RelativeSource={RelativeSource Self}}` 让媒体区塌成细条。必须在控件内部按 `ActualWidth` 设。
- **文字标题可以绑定，但控件化后改用属性赋值**，以便语言切换时就地重发。
- **不改 WindowsAppSDK 版本**（`Microsoft.WindowsAppSDK 2.5.1`、`WindowsAppSDK.WinUI 2.3.9` 均不动）。
- **不换 `ItemsRepeater` / `UniformGridLayout`。**
- **`TrackCount` 用 `int?`，未知是 `null`，不是 `0`。**
- **`AlbumTitle` 撞名修在页面侧**：改 `MpvPlayerPage.xaml` 里专辑详情页那个 `x:Name`，不动 `LibraryEntry.AlbumTitle`。
- 构建必须用全名参数：`.\build.ps1 -Configuration Release -Platform x64`（`-Release x64` 会静默退回 Debug）。
- 改 XAML 前先跑 `python tools\check-xaml.py`；提交前跑 BOM 扫描与 `grep -rn "\[DBG-"`。
- 只暂存仓库文件，绝不 `bin/`、`obj/`、`dist/`、`libs/`。

## Review Focus

spec 暗示、但没有任务的测试直接覆盖、且最容易咬到真实用户的五类：

1. **空库与单条曲目。** 一个没有标签的 flac、一个只有一条曲目的专辑：`TrackCount` 为 `null` 与为 `1` 时卡片的呈现，以及分组头在零曲目时的表现。
2. **卡片回收后状态串台。** 滚动让同一个容器指向另一条曲目时，色条、选中框、封面必须跟着新条目走，不能留着上一条的。
3. **标签晚到与重建。** `LoadMusicTagsAsync` 之后 `ApplyLibraryFilter()` 重建整棵树：`IsNowPlaying` 必须存活，否则色条在用户眼前消失。
4. **封面始终缺失。** 没有封面时回退图标必须一直可见，且不能因为代码路径把 `Cover` 设成 `null` 又设成非空而闪烁。
5. **超长标题与高 DPI。** 两行标题在字符裁切下的表现；卡片在 175% 缩放下方形不得失真。

---

## File Structure

| 文件 | 职责 | 动作 |
|---|---|---|
| `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml` | 卡片的视觉树：封面区、回退图标、悬停遮罩、两个角按钮、两行文字、色条 | 新建 |
| `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml.cs` | 类型化属性与状态机；无绑定、无 `FindDescendant` | 新建 |
| `src/mpv-app/Domains/Playback/Cards/CardMetrics.cs` | 卡片几何令牌的纯类（宽高比、圆角、行距、按钮尺寸、色条高） | 新建 |
| `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs` | `ElementPrepared` 改为向 `LibraryCard` 赋属性；删除 `SetCardCaption` / `SetCardCover` / `SetCardMediaSquare` / `FindDescendant` / `FindCardOverlay` / `ApplyCardOverlayStrings` | 改 |
| `src/mpv-app/Domains/Playback/MpvPlayerPage.xaml` | 删两个卡片 `DataTemplate` 与 `LibraryCardButtonStyle` / `LibraryCardMediaStyle`；`LibraryCardTemplate` 与 `AlbumCardTemplate` 资源键换成同一个控件模板；专辑详情页 `x:Name="AlbumTitle"` 改名 | 改 |
| `src/mpv-app/Domains/Playback/LibraryEntry.cs` | 新增 `TrackCount`（`int?`）、`IsNowPlaying`（`bool`），两者均 raise | 改 |
| `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Music.cs` | 回填 `TrackCount`；换用 `MusicGroupHeaderTemplate`；`BuildMusicTagGroups` 传封面 | 改 |
| `src/mpv-app/Domains/Playback/MusicGroupHeader.cs` | 音乐分组头的数据对象（名称、封面、曲目数、是否正在播放） | 新建 |
| `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs` | 正在播放的设置点（`PlayLibraryEntry` 与打开路径） | 改 |
| `tests/conf/CardMetricsTests.cs` | 几何令牌的纯逻辑断言 | 新建 |
| `tests/conf/LibraryEntryTrackCountTests.cs` | `TrackCount` 的 `null` vs `0` 语义 | 新建 |
| `tests/conf/mpv-conf-test.csproj` | 链接两个新纯类 | 改 |
| `tools/ui-design-audit.py` | 新增 `cards` board | 改 |
| `tools/check-xaml.py` | 新增一条：页面内 `x:Name` 不得与 `LibraryEntry` 公开属性同名 | 改 |
| `docs/research/06-image-cards.md` | 更正 `WMC9999` 的错误归因 | 改 |

---

## Task 1: 更正 WMC9999 的错误归因（先做，因为它会误导后面每一步）

**Files:**
- Modify: `docs/research/06-image-cards.md:1-30`
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.xaml:337-361`（`AlbumCardTemplate` 顶部那段注释）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs:762-806, 910-923, 952-964`

**Interfaces:**
- Consumes: 无
- Produces: 无（纯文档与注释）

- [ ] **Step 1: 把实测的对照结果写进 `docs/research/06-image-cards.md`**

在文件开头的更正声明之后加一节，内容是下面这张表（数值照抄，它们来自 2026-10-31 在 `644a955` 上的实测）：

| 试验 | 结果 |
|---|---|
| `{x:Bind AlbumArtistText}` | 编译通过 |
| `{x:Bind AlbumTitle}` | WMC9999 |
| 先把 `MpvPlayerPage.xaml` 里专辑详情页的 `x:Name="AlbumTitle"` 改名，再 `{x:Bind AlbumTitle}` | 编译通过，运行时渲染出 `i Love` |
| `{x:Bind Thumbnail}` 取代 `SetCardCover` | 编译通过，运行时封面不显示（`stddev 20.4 / 25 色`；原代码同一张卡 `57.5 / 52258 色`） |

结论两句话：文字标题的崩溃来自页面字段撞名，不是 `{x:Bind}` 本身；封面的失败是绑定真的交付不了 `ImageSource`。

- [ ] **Step 2: 改掉三处代码注释里的错误归因**

`MpvPlayerPage.xaml` 里 `AlbumCardTemplate` 顶部那段（"A single `{x:Bind AlbumTitle}` anywhere in this file makes the WindowsAppSDK 2.3.9 XAML compiler fail with WMC9999"）与 `MpvPlayerPage.Library.Cards.cs` 里三处同义说法，改成撞名的表述。改完这三处后 Task 2 会把它们整体删掉——这一步先改是为了让 Task 2 的 diff 只包含删除，不混着改注释。

- [ ] **Step 3: 跑文档检查器**

Run: `python tools\check-doc-references.py && python tools\check-doc-tooling.py && python tools\check-doc-freshness.py`
Expected: 三个都 OK。注意 `check-doc-ranges.py` 现在是**红的**（2 条来自 `settings-contract.md` 的既有失效引用，与本次无关），不要试图修它。

- [ ] **Step 4: Commit**

```bash
git add docs/research/06-image-cards.md src/mpv-app/Domains/Playback/MpvPlayerPage.xaml src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs
git commit -m "docs: WMC9999 comes from a page field called AlbumTitle, not from {x:Bind}"
```

---

## Task 2: 卡片几何令牌（纯类，可 TDD）

**Files:**
- Create: `src/mpv-app/Domains/Playback/Cards/CardMetrics.cs`
- Create: `tests/conf/CardMetricsTests.cs`
- Modify: `tests/conf/mpv-conf-test.csproj`

**Interfaces:**
- Consumes: 无
- Produces: `mpv_winui.Domains.Playback.Cards.CardMetrics`，静态类，成员：
  - `double CoverAspect(bool isAlbum)` — 专辑 `1.0`，视频 `16.0/9.0`
  - `double CoverHeightFor(double width, bool isAlbum)` — `width / CoverAspect(isAlbum)`
  - `double CornerRadius` — `4`
  - `double CaptionTopGap` — `8`
  - `double CaptionLineGap` — `2`
  - `double PlayButtonSize` — `40`
  - `double MoreButtonSize` — `32`
  - `double CornerInset` — `10`
  - `double NowPlayingBarHeight` — `2`

- [ ] **Step 1: 写失败的测试**

```csharp
using mpv_winui.Domains.Playback.Cards;

namespace mpv_winui_test;

[TestFixture]
public class CardMetricsTests
{
    [Test]
    public void AlbumCoverIsSquare()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: true), Is.EqualTo(1.0));
    }

    [Test]
    public void VideoCoverIsSixteenByNine()
    {
        Assert.That(CardMetrics.CoverAspect(isAlbum: false),
            Is.EqualTo(16.0 / 9.0).Within(1e-9));
    }

    [Test]
    public void AlbumCoverHeightEqualsItsWidth()
    {
        Assert.That(CardMetrics.CoverHeightFor(170, isAlbum: true), Is.EqualTo(170).Within(1e-9));
    }

    [Test]
    public void VideoCoverHeightIsWidthOverAspect()
    {
        // 253 through 16:9 is 142.31, which is the height the markup comment claims.
        Assert.That(CardMetrics.CoverHeightFor(253, isAlbum: false), Is.EqualTo(142.3125).Within(1e-3));
    }

    [Test]
    public void ZeroWidthStaysZero()
    {
        // A recycled card reports 0 before layout; a NaN or negative height here
        // would throw inside the layout pass rather than draw nothing.
        Assert.That(CardMetrics.CoverHeightFor(0, isAlbum: true), Is.EqualTo(0));
    }

    [Test]
    public void TokensAreTheDocumentedValues()
    {
        Assert.That(CardMetrics.CornerRadius, Is.EqualTo(4));
        Assert.That(CardMetrics.CaptionTopGap, Is.EqualTo(8));
        Assert.That(CardMetrics.CaptionLineGap, Is.EqualTo(2));
        Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(40));
        Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(32));
        Assert.That(CardMetrics.CornerInset, Is.EqualTo(10));
        Assert.That(CardMetrics.NowPlayingBarHeight, Is.EqualTo(2));
    }
}
```

- [ ] **Step 2: 把它链接进测试工程**

在 `tests/conf/mpv-conf-test.csproj` 里，紧跟 `LibraryIndexLetter.cs` 那条后面加：

```xml
<Compile Include="..\..\src\mpvw-app\Domains\Playback\Cards\**\*.cs" Link="Player\Cards\%(RecursiveDir)%(Filename)%(Extension)" />
```

- [ ] **Step 3: 跑测试确认它失败**

Run: `dotnet build tests\conf\mpv-conf-test.csproj -c Release --no-incremental -v:minimal`
Expected: FAIL，`CS0246: 未能找到类型或命名空间名 CardMetrics`。

- [ ] **Step 4: 实现 `CardMetrics`**

`src/mpv-app/Domains/Playback/Cards/CardMetrics.cs`，`namespace mpv_winui.Domains.Playback.Cards`，`internal static class CardMetrics`。`CoverHeightFor` 必须在 `width <= 0` 时返回 `0`，不返回负数或 NaN。

每个成员上方一行注释说明它替代了哪个字面量（例如 `PlayButtonSize` 替代 `AlbumCardTemplate` 里的 `Width="48"` 与视频卡的同一行）。

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet build tests\conf\mpv-conf-test.csproj -c Release --no-incremental -v:minimal && dotnet test tests\conf\mpv-conf-test.csproj -c Release --no-build -v:minimal`
Expected: 通过，527 + 6 = 533。

- [ ] **Step 6: Commit**

```bash
git add src/mpv-app/Domains/Playback/Cards/CardMetrics.cs tests/conf/CardMetricsTests.cs tests/conf/mpv-conf-test.csproj
git commit -m "cards: the card geometry gets one home, and it is not two DataTemplates"
```

---

## Task 3: `LibraryCard` 控件（本计划的核心）

**Files:**
- Create: `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml`
- Create: `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml.cs`
- Test: `tools/ui-design-audit.py` 的 `cards` board（见 Task 6），本任务先手工验证

**Interfaces:**
- Consumes: `CardMetrics`（Task 2）
- Produces: `mpv_winui.Domains.Playback.Controls.LibraryCard : UserControl`，成员：
  - `void SetEntry(LibraryEntry? entry)` — 唯一入口，把所有状态一次性写进视觉树
  - `LibraryEntry? Entry { get; }`
  - `bool IsAlbum { get; set; }` — 决定宽高比与是否显示曲目数
  - 事件：`event EventHandler? PlayRequested;` `event EventHandler? MoreRequested;` `event EventHandler? CardTapped;`
  - 内部部件名（只在本控件内可见）：`CoverImage`、`FallbackIcon`、`MediaBorder`、`HoverOverlay`、`PlayButton`、`MoreButton`、`TitleText`、`SubtitleText`、`SelectionBox`、`NowPlayingBar`

- [ ] **Step 1: 写 `LibraryCard.xaml`**

结构（照此顺序，不要发明第四层）：

```
<UserControl>
  <Button x:Name="RootButton">            <!-- 整卡可点，带键盘焦点与 UIA 名字 -->
    <StackPanel>
      <Border x:Name="MediaBorder" Style=...>   <!-- 圆角、背景取自 CardMetrics -->
        <Grid>
          <Viewbox x:Name="FallbackIcon">...<PathIcon x:Name="FallbackGlyph"/></Viewbox>
          <Image x:Name="CoverImage" Stretch="UniformToFill" Visibility="Collapsed"/>
          <Rectangle x:Name="NowPlayingBar" Height="2" VerticalAlignment="Bottom" Fill="{ThemeResource AccentFillColorDefaultBrush}" Visibility="Collapsed"/>
          <Grid x:Name="HoverOverlay" Visibility="Collapsed" IsHitTestVisible="False">
            <Border><!-- 渐变遮罩：底部 SmokeFillColorDefaultBrush，顶部透明 --></Border>
            <Button x:Name="PlayButton" HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="10"/>
            <Button x:Name="MoreButton" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="10"/>
            <Border x:Name="SelectionBox" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="8"/>
          </Grid>
        </Grid>
      </Border>
      <StackPanel Margin="0,8,0,0" Spacing="2">
        <TextBlock x:Name="TitleText" MaxLines="2" TextWrapping="Wrap" TextTrimming="CharacterEllipsis"/>
        <TextBlock x:Name="SubtitleText" TextTrimming="CharacterEllipsis"/>
      </StackPanel>
    </StackPanel>
  </Button>
</UserControl>
```

XAML 注释里**不得出现 `--`**（WMC9997）。写之前先读 `check-xaml.py` 的报错文案。

- [ ] **Step 2: 写 `LibraryCard.xaml.cs` 的骨架**

构造函数里 `InitializeComponent()`；`MediaBorder.SizeChanged += (s, e) => ApplySquare();`；`ApplySquare()` 用 `CardMetrics.CoverHeightFor(MediaBorder.ActualWidth, IsAlbum)` 设 `MediaBorder.Height`。

`SetEntry` 做这些事，顺序固定：

1. `Entry = entry`；`DataContext = entry`（`CardTapped` 等事件需要它，见 Task 4）
2. `TitleText.Text` / `SubtitleText.Text` 从 entry 取（专辑用 `AlbumTitle`/`AlbumArtistText`，视频用 `Title`/`Subtitle`，由 `IsAlbum` 决定）
3. 封面：`entry?.Thumbnail` 有值就用，否则查缓存（缓存查找留在页面侧，见 Task 4 的 `CoverProvider`），都没有则 `CoverImage.Visibility=Collapsed` 且 `FallbackIcon.Visibility=Visible`
4. `FallbackGlyph.Data = entry.MediaIcon`（每次新建 Geometry，见 `LibraryEntry.cs:107-117` 的规则）
5. 正在播放色条的可见性取自 `entry.IsNowPlaying`
6. 选中框取自 `entry.SelectionVisibility` 与 `entry.SelectionGlyph`
7. `AutomationProperties.SetName(RootButton, TitleText.Text)`
8. 订阅 `entry.PropertyChanged`（一次性：`SetEntry` 开头先退订上一条）

- [ ] **Step 3: 手工验证：卡片外观不变**

把 `AlbumCardTemplate` 先保留，只在 `BuildMusicTagGroups` 里把模板换成 `new DataTemplate { }` 不可行——`ItemsRepeater` 需要 DataTemplate。做法：加一个 `<DataTemplate x:Key="LibraryCardControlTemplate"><controls:LibraryCard/></DataTemplate>`，然后 `BuildMusicTagGroups` 用它，`ElementPrepared` 里加 `if (args.Element is LibraryCard c) c.SetEntry(view.GetAt(args.Index) as LibraryEntry);`。

Run: `python tools\check-xaml.py && dotnet build src\mpvw-app\mpv-winui.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false -v:m`
Expected: 退出码 0，只有 2 条 `WMC1510`。

启动应用、进音乐库 → 专辑页签，确认：封面显示、两行文字正确、悬停出现遮罩与两个按钮。**这一步的验收标准是「和改之前一样」**——Task 5 才动外观。

三件 Review Focus 里的事在这一步手工确认，并把结果记进提交信息（它们都是**回收与缺失**路径，改这一层最容易踩）：

1. **回收后串台**：专辑页签只有一张卡时，切到「歌曲」页签（多张卡）再切回「专辑」。封面、标题、回退图标不得留着歌曲页签那几张的值。
2. **封面一直缺失**：临时把 `SetEntry` 里的封面查询改成永远返回 `null`，确认回退图标可见且有颜色可辨；改回来。**不要提交这个改动。**
3. **高 DPI**：把窗口在显示器间拖到 175% 缩放的那一屏（本机 `windowDpi:168, scale:1.75`，见 `docs/runtime-facts.md` §3），确认封面区仍是正方形——用 `winapp ui inspect` 读卡片 Button 的 `width`/`height`，两者相差应小于 2px 的标题行高之外的部分（即媒体区边长等于卡宽）。

- [ ] **Step 4: Commit**

```bash
git add src/mpv-app/Domains/Playback/Controls/ src/mpv-app/Domains/Playback/MpvPlayerPage.xaml src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Music.cs src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs
git commit -m "cards: the album card is a control with fields, not a template searched by name"
```

---

## Task 4: 页面侧接线与删除旧机制

**Files:**
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs`（删 `SetCardCaption` / `SetCardCover` / `SetCardMediaSquare` / `FindDescendant` / `FindCardOverlay` / `ApplyCardOverlayStrings` / `ResetCardOverlay` / `ShowCardOverlay` / `HideCardOverlay` / `HideCardOf` / `_activeCardOverlay`）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.xaml`（删两个卡片 `DataTemplate`、`LibraryCardButtonStyle`、`LibraryCardMediaStyle`）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Video.cs`（视频库也换控件模板）

**Interfaces:**
- Consumes: `LibraryCard`（Task 3）的 `SetEntry` / `IsAlbum` / 三个事件
- Produces: `LibraryCardCoverProvider` —— 页面实现的一个 `Func<LibraryEntry, ImageSource?>`，负责「先缓存、后 entry」的封面查询（`MpvPlayerPage.Library.Cards.cs:853-863` 那段顺序是实测出来的，必须原样保留）

- [ ] **Step 1: 把封面缓存查询抽成一个方法**

`private ImageSource? ResolveCover(LibraryEntry entry)`：先 `entry.Thumbnail`，再 `_libraryThumbnails.TryGetValue(entry.Path, out var cached)`。注释照抄原文的理由（回收卡片早于数据到达）。

- [ ] **Step 2: `ElementPrepared` 改成纯赋值**

只剩：`if (args.Element is LibraryCard card) { card.IsAlbum = <由 section/grouping 决定>; card.SetEntry(entry); }` 加 `MotionBehaviors.PosterEnter`。**不再有 `FindCardOverlay` / `ResetCardOverlay` / `ApplyCardOverlayStrings` / `DataContext = item` 的手动写入**——退订/重订阅、遮罩复位、无障碍名都归 `SetEntry`。

- [ ] **Step 3: 把三个事件接回页面**

`PlayRequested` → `PlayLibraryEntry(card.Entry)`；`MoreRequested` → `_cardMenuEntry = card.Entry; ShowCardContextMenu(card.MoreButton)`（把 `CardMore_Click` 的后半段抽成这个方法，菜单项的启用逻辑不动）；`CardTapped` → 原 `LibraryCard_Click` 的正文。

- [ ] **Step 4: 删掉旧机制**

`MpvPlayerPage.xaml` 删 `LibraryCardTemplate`、`AlbumCardTemplate` 两个 `DataTemplate` 与 `LibraryCardButtonStyle`、`LibraryCardMediaStyle`。`MpvPlayerPage.Library.Cards.cs` 删上面列出的方法。删之前 `grep -rn "FindDescendant" src/` 确认无其他调用者。

**结构性删除用脚本，不要用 edit 猜锚点**（仓库教训：`edit` 的 `old_string` 写错差点毁掉 `OptionControlBase.cs`）。

- [ ] **Step 5: 全量检查**

Run:
```
python tools\check-xaml.py
python tools\check-resources.py
python tools\check-scroll-maxwidth.py
python tools\check-typography-standard.py
python tools\check-ui-tooltips.py
.\build.ps1 -Configuration Release -Platform x64
```
Expected: 除 `check-doc-ranges.py`（既有 2 条失败）外全绿；构建退出码 0；`527 + 6 = 533` 与 `26` 全过；警告 2 条。

- [ ] **Step 6: Commit**

```bash
git add -A src/mpv-app/Domains/Playback/
git commit -m "cards: the name search is gone, and with it four ways to fail silently"
```

---

## Task 5: 卡片视觉（悬停态、呼吸、两模板合一）

**Files:**
- Modify: `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml`
- Modify: `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml.cs`
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Music.cs:535-541`（`AlbumCardLayout`）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Video.cs:335-341`（`CardLayout`）

**Interfaces:**
- Consumes: `CardMetrics`、`LibraryCard`
- Produces: 无新公开接口

- [ ] **Step 1: 悬停态回到角上**

`PlayButton`：`HorizontalAlignment="Left"`、`VerticalAlignment="Bottom"`、`Margin="10"`、`Width/Height = CardMetrics.PlayButtonSize`。
`MoreButton`：`Right` + `Top` + `Margin="10"` + `CardMetrics.MoreButtonSize`。
遮罩 `Border`：`Background` 换成线性渐变——底边 `SmokeFillColorDefaultBrush` 全不透明，顶部 `Transparent`（`LinearGradientBrush`，`StartPoint="0,1"` `EndPoint="0,0"`）。

- [ ] **Step 2: 呼吸**

`StackPanel` 的 `Margin="0,8,0,0"`（= `CardMetrics.CaptionTopGap`）、`Spacing="2"`（= `CardMetrics.CaptionLineGap`）。`TitleText` 用 `BodyTextBlockStyle`，`SubtitleText` 用 `CaptionTextBlockStyle` 且 `Foreground="{ThemeResource TextFillColorSecondaryBrush}"`。

- [ ] **Step 3: 两个布局方法调用同一个令牌**

`AlbumCardLayout()` 与 `CardLayout()` 各自的 `MinItemWidth` / `MinColumnSpacing` / `MinRowSpacing` 保持不变（它们是**布局**参数、不是卡片几何），但 `MinItemHeight` 改成 `CardMetrics.CoverHeightFor(MinItemWidth, isAlbum) + 两个文字行高 + CaptionTopGap`。两个方法体里的魔数消失。

- [ ] **Step 4: 目视 + 像素验证**

启动应用，进专辑页签，截图；再进视频库，截图。确认：播放键在左下、更多键在右上、遮罩是底部渐变；封面上没有按钮压着主体。

Run: `python tools\ui-design-audit.py --board cards --screenshot specs/shots/cards-after.png`

再确认 Review Focus 第 5 条的另一半：把标题改成一个 60 字的长度（临时改 `BuildMusicTagGroups` 里的分组名，别提交），确认两行之后按字符裁切、不溢出卡片、不把副标题顶出可视区。

- [ ] **Step 5: Commit**

```bash
git add src/mpv-app/Domains/Playback/ specs/shots/
git commit -m "cards: the play button goes back to the corner, and the scrim becomes a gradient"
```

---

## Task 6: `cards` board（先看它红）

**Files:**
- Modify: `tools/ui-design-audit.py`

**Interfaces:**
- Consumes: 运行中的应用
- Produces: `BOARDS["cards"]`

- [ ] **Step 1: 写 board，用当前（未修）的代码先跑一次**

断言表（数据，不是代码）：

```python
BOARDS["cards"] = {
    "open": "ShellNav_music",
    "open_caption": "专辑",
    "assertions": [
        # A card is one button whose accessible name is its caption. It was an
        # empty string while the template bound {Binding AlbumTitle}, because
        # ItemsRepeater does not set a DataContext.
        ("card_names_nonempty", None, None),
        # The hover overlay's two buttons sit in the two lower/upper corners,
        # not in the middle: measured against the card, the play button's
        # centre must be in the left half and below the middle.
        ("play_button_not_centred", None, None),
        # A cover that loaded has colour in it. The historical "cover did not
        # show" measurement was stddev 0.0 / 1 colour.
        ("cover_has_colour", None, 12.0),
    ],
}
```

`evaluate` 里为这三种 kind 各加一个分支；判据写进注释，数值写进断言表。

- [ ] **Step 2: 确认它在未做题的状态下红**

在 Task 3、4、5 落地**之前**单独跑一次（可以先 `git stash` 掉 C1-C2 的改动），Expected: `card_names_nonempty` FAIL、`play_button_not_centred` FAIL。**两个都红**才算这条断言有效。如果 `play_button_not_centred` 在改前是绿的，说明判据写错了。

- [ ] **Step 3: 恢复改动，再跑一次**

Expected: 三条全绿。

- [ ] **Step 4: Commit**

```bash
git add tools/ui-design-audit.py
git commit -m "audit: a cards board, watched red on the old layout first"
```

---

## Task 7: 信息位（`TrackCount` / `IsNowPlaying`）

**Files:**
- Modify: `src/mpv-app/Domains/Playback/LibraryEntry.cs`
- Create: `tests/conf/LibraryEntryTrackCountTests.cs`
- Modify: `src/mpv-app/Domains/Playback/Controls/LibraryCard.xaml(.cs)`
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Music.cs`（回填）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs`（设置正在播放）

**Interfaces:**
- Consumes: `LibraryCard.SetEntry`
- Produces:
  - `LibraryEntry.TrackCount` — `int?`，`init`，`null` = 未知
  - `LibraryEntry.IsNowPlaying` — `bool`，`internal set`，raise `PropertyChanged`
  - `LibraryEntry.SetNowPlaying(bool)` — 页面调用

- [ ] **Step 1: 先确认一件已经验证过的事**

`LibraryEntry` **不能**链接进 `tests/conf`：它 `using Microsoft.UI.Xaml`，而那个工程的 `TargetFramework` 是 `net10.0`。实测（2026-10-31）链接后立刻报 `CS0234: 命名空间 Microsoft 中不存在类型 UI` 与 `CS0246: Visibility`。

所以 `TrackCount` 的语义必须落在一个**不依赖 WinUI 的纯类**里，否则它只能靠 board 断言，等于没有单元测试。

- [ ] **Step 2: 写失败的测试（针对纯类）**

`tests/conf/AlbumTrackCountTests.cs`，被测类型是 `mpv_winui.Domains.Playback.Cards.AlbumTrackCount`（Task 2 那个 `Cards` 目录里新增一个文件，已被 `Cards\**\*.cs` 的链接覆盖）：

```csharp
using mpv_winui.Domains.Playback.Cards;

namespace mpv_winui_test;

[TestFixture]
public class AlbumTrackCountTests
{
    [Test]
    public void UnknownIsNullNotZero()
    {
        // 0 would claim the album has no tracks, which is a different fact
        // from "the tags have not been read yet".
        Assert.That(AlbumTrackCount.For(0), Is.Null);
    }

    [Test]
    public void OneTrackIsOne()
    {
        Assert.That(AlbumTrackCount.For(1), Is.EqualTo(1));
    }

    [Test]
    public void ManyTracksKeepTheirCount()
    {
        Assert.That(AlbumTrackCount.For(13), Is.EqualTo(13));
    }

    [Test]
    public void NegativeCannotHappenAndReadsAsUnknown()
    {
        Assert.That(AlbumTrackCount.For(-1), Is.Null);
    }

    [Test]
    public void LabelIsEmptyWhenCountIsUnknown()
    {
        // The caption must not render "· 0 首" or a bare separator.
        Assert.That(AlbumTrackCount.LabelFor(null, n => $"{n} 首"), Is.Empty);
    }

    [Test]
    public void LabelUsesTheSuppliedFormatter()
    {
        Assert.That(AlbumTrackCount.LabelFor(7, n => $"{n} 首"), Is.EqualTo("7 首"));
    }
}
```

`LabelFor(int? count, Func<int, string> format)` 的存在理由：本地化文案要由调用方从 `AppLang` 取，纯类不许碰 `AppContext`。

- [ ] **Step 3: 跑它，确认失败**

Run: `dotnet build tests\conf\mpv-conf-test.csproj -c Release --no-incremental -v:minimal`
Expected: FAIL，`CS0246: 未能找到类型或命名空间名 AlbumTrackCount`。

- [ ] **Step 4: 实现 `AlbumTrackCount`**

`For(int) -> int?`：`<= 0` 返回 `null`，否则返回该值。
`LabelFor(int?, Func<int,string>) -> string`：`null` 返回 `string.Empty`，否则返回 `format(count.Value)`。

它同时覆盖 Review Focus 第 1 条（空库与单条曲目）：`For(0)` 是「标签还没读」，`For(1)` 是「一张只有一条曲目的专辑」，两者在卡片上的呈现不同，而这条规则现在有六个断言盯着。

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet build tests\conf\mpv-conf-test.csproj -c Release --no-incremental -v:minimal && dotnet test tests\conf\mpv-conf-test.csproj -c Release --no-build -v:minimal`
Expected: 通过，527 + 6（Task 2）+ 6（本任务）= 539。

- [ ] **Step 6: `LibraryEntry` 转发这个语义**

`public int? TrackCount { get; init; }` —— 由回填代码调用 `AlbumTrackCount.For(groupCount)` 赋值，`LibraryEntry` 自己不判断 0。

- [ ] **Step 7: Commit**

```bash
git add src/mpv-app/Domains/Playback/Cards/AlbumTrackCount.cs tests/conf/AlbumTrackCountTests.cs src/mpv-app/Domains/Playback/LibraryEntry.cs
git commit -m "cards: an unknown track count is null, and that rule has a test"
```

- [ ] **Step 8: 回填 `TrackCount`**

`LoadMusicTagsAsync` 在 `for` 循环之后、`ApplyLibraryFilter()` 之前：按 album 名分组，组内条数写进每一条 entry 的副本（`replaced` 是新实例，所以要在循环里就把分组结果算好再写，或者循环后建一张 `album -> count` 表，第二次替换）。

- [ ] **Step 9: 搬运状态**

`LoadMusicTagsAsync` 与 `FromHistory` 重建 entry 时，`IsNowPlaying` 要与 `Thumbnail` / `IsSelected` 一样被搬运（`MpvPlayerPage.Library.Music.cs:470-489` 已经有两个先例）。**漏掉这一步的症状是：标签读完、树重建，色条在用户眼前消失。**

- [ ] **Step 10: 卡片显示**

`IsAlbum && Entry.TrackCount is { } n` 时，副标题行变成 `Artist · n 首`（用 `AppLang` 的键，不硬编码中文；`check-localization.py` 会核对 9 份文件）。拼接走 `AlbumTrackCount.LabelFor`。

- [ ] **Step 11: 正在播放色条**

`SetNowPlaying` 由一个页面方法统一调用：播放/打开某条 entry 时先把旧的置 false、新的置 true。色条与标题颜色在 `LibraryCard` 的 `PropertyChanged` 处理里刷新。

- [ ] **Step 12: 验证（含 Review Focus 第 2、3 条）**

Run: `python tools\ui-design-audit.py --board cards`

再手工走这三条，任一条不成立就停下修：

1. **播放态正确**：播放一首歌，色条出现在正确的卡上；换一首，色条移动到新的那张、旧的消失（不是两张都亮）。
2. **回收不串台**：滚到列表底部再滚回顶部，色条仍在正确的那张上，不会跟着容器跑到别的卡。
3. **重建后存活**：这是本任务最易漏的一条——让标签读取重新跑一次（切走再切回音乐库即可触发 `LoadLibraryAsync`）。`ApplyLibraryFilter` 会重建整棵树；重建后色条必须仍在同一张卡上。**不成立就是 Step 9 的搬运漏了。**

- [ ] **Step 13: Commit**

```bash
git add -A src/mpv-app/Domains/Playback/ tests/conf/
git commit -m "cards: the playing card says so, and its track count survives a rebuild"
```

---

## Task 8: 音乐库分组头

**Files:**
- Create: `src/mpv-app/Domains/Playback/MusicGroupHeader.cs`
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.xaml`（新增 `MusicGroupHeaderTemplate`）
- Modify: `src/mpv-app/Domains/Playback/MpvPlayerPage.Library.Music.cs:343-423`

**Interfaces:**
- Consumes: `LibraryEntry.TrackCount`（Task 7）
- Produces: `MusicGroupHeader` —— `Name`(string)、`TrackCount`(int?)、`Cover`(ImageSource?)、`Letter`(string)

- [ ] **Step 1: 写数据对象**

纯类，无 WinUI 依赖（除 `ImageSource?`，可改成 `object?` 以便测试）。`Name` / `Letter` / `TrackCount` 为 `get; init;`。

- [ ] **Step 2: 写模板**

`MusicGroupHeaderTemplate`：一行 `Grid` —— 48x48 圆角封面（回退图标）+ 名称（`BodyStrongTextBlockStyle`）+ 曲目数（`CaptionTextBlockStyle`，未知时不渲染）。**不要**复用 `VideoFolderHeaderTemplate`。

- [ ] **Step 3: `BuildMusicTagGroups` 改用它**

`var headerTemplate = (DataTemplate)Resources["MusicGroupHeaderTemplate"];`，`Content` 从 `VideoFolderGroup` 换成 `MusicGroupHeader`（封面取组内第一条有 `Thumbnail` 的 entry，曲目数取组内 `Count`）。

- [ ] **Step 4: 首字母导轨不再画空字母**

这一条属于 §8.5-2 但只需一处改动：导轨已按 `HasContent` 调透明度与 `IsEnabled`（`MpvPlayerPage.xaml:317-335`）。确认它在音乐分组下真的灰掉了；若没有，修 `BuildLibraryIndexRail` 的入参。

- [ ] **Step 5: 验证**

Run: `python tools\check-sections.py`（若它扫到音乐分区）+ `.\build.ps1` + 手工进三个页签各看一眼。分组头不得再有空白路径行。

- [ ] **Step 6: Commit**

```bash
git add -A src/mpv-app/Domains/Playback/
git commit -m "music: the album groups get a header of their own instead of the folder one"
```

---

## Task 9: 防复发检查器

**Files:**
- Modify: `tools/check-xaml.py`

**Interfaces:**
- Consumes: `LibraryEntry.cs` 的公开属性名
- Produces: 一条新的检查

- [ ] **Step 1: 先看它红**

在 `MpvPlayerPage.xaml` 里临时把某个 `x:Name` 改成 `AlbumTitle`，跑检查器，**必须报错**。不红就说明正则或扫描根写错了。

- [ ] **Step 2: 实现**

规则：页面 XAML 里每个 `x:Name` 不得与 `LibraryEntry` 的公开属性名相同（数据源类型与页面字段撞名正是 WMC9999 的来源）。扫描根用 `APP_DIR.rglob("*.xaml")` 的写法，与 `check-icon-standard.py` 一致；匹配到 0 个页面时必须报错而不是报成功（仓库规矩：拒绝不可信输入）。

- [ ] **Step 3: 恢复临时改动，确认全绿**

- [ ] **Step 4: Commit**

```bash
git add tools/check-xaml.py
git commit -m "checker: a page field may not be named after a LibraryEntry property"
```

---

## Task 10: 收尾

- [ ] **Step 1: 跑全量**

```powershell
python tools\check-xaml.py
python tools\check-resources.py
python tools\check-scroll-maxwidth.py
python tools\check-typography-standard.py
python tools\check-ui-tooltips.py
python tools\check-localization.py
python tools\check-doc-references.py
python tools\check-doc-tooling.py
python tools\check-doc-freshness.py
python tools\check-build-warnings.py
.\build.ps1 -Configuration Release -Platform x64
python tools\ui-design-audit.py --board cards
python tools\ui-design-audit.py --board settings
```

Expected: 全绿（`check-doc-ranges.py` 除外，既有 2 条失败已记录）；`533 + 26`；警告 2 条；两个 board 都通过。

- [ ] **Step 2: BOM / 换行 / 调试插桩**

```powershell
git diff --check
grep -rn "\[DBG-" src/ ; # 必须为空
git diff --stat | # 不得有整文件重写
```

- [ ] **Step 3: 更新 `docs/`（只写事实，不写计划）**

`docs/research/06-image-cards.md` 的更正已在 Task 1 做。若 C1 改变了「`ItemsRepeater` 不设 DataContext」的那条结论（现在无关紧要，因为不再用 `{Binding}`），把那段改成历史说明。

- [ ] **Step 4: Commit**

```bash
git add -A docs/
git commit -m "docs: the card research says what the measurements say"
```
