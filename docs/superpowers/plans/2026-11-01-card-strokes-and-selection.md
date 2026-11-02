# 卡片描边与选择模式重做 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把卡片的三层描边（本体常态、悬浮外框、选中保留）改成不参与布局的半透明描边并支持深浅色，把选择模式从独立开关改成「有勾选即在模式里」，并把命名播放列表作为侧栏分区加回来。

**Architecture:** 卡片几何继续由 `CardMetrics`（纯类，可单测）与 `LibraryCard.xaml` 里被断言钉住的字面量共同承载，两边一起改。描边颜色新增三个主题令牌进 `Styles.xaml` 的三套字典，引用用 `ThemeResource`，不动既有的 `MpvCardRingBrush`。选择模式改由选中数驱动，`UpdateSelectionBar` 成为唯一推进点。播放列表用每列表一个 `.m3u`，纯解析/格式化逻辑与文件 IO 分开，前者可单测。

**Tech Stack:** WinUI 3 / WindowsAppSDK 2.5.1（WinUI 子包 2.3.9）、net10.0-windows10.0.26100.0、C#、NUnit 4（`tests/conf`，plain net10.0，不能引用 Microsoft.UI.Xaml）、Python（检查器）。

**Spec:** `specs/2026-11-01-card-strokes-and-selection-design.md`

## Global Constraints

以下每条来自 spec，逐字照抄，每个任务均隐含包含：

- **不用模糊。** 合成层 `DropShadow`、多层 Border 模拟模糊、`ThemeShadow` 三种做法都已被用户否决。
- **外框不参与布局。** 悬浮前后，卡片内容（缩略图 + 文字）坐标必须逐像素一致。
- **`MpvCardRingBrush` 不动**（`Styles.xaml:1454`，`#E6FFFFFF`）。它兼着播放页 OSD 的图标与文字（`MpvPlayerPage.xaml:979,987`）。
- **新增三个令牌**：`MpvCardFrameBrush`、`MpvCardButtonRingBrush`、`MpvCardCoverStrokeBrush`，各写 Light / Dark / HighContrast 三套。
- **浅色反转成黑墨**，透明度关系与深色相同（8% / 20% / 45%）。
- **模式 ≡ 选中数 > 0**，不是独立标志位。
- **专辑卡不出勾选框**（Albums / Artists 页）。
- **`MpvSpaceM` / `MpvSpaceL` 的值不许改**（各被 25 处 markup 引用）。卡片网格的 20/20 用新常量 + 字面量。
- **播放列表路径必须走 `AppData.Current.ResolveLocalData`**，不许手拼 `%LOCALAPPDATA%`。
- **构建用全名参数**：`.\build.ps1 -Configuration Release -Platform x64`（`-Release x64` 会静默退回 Debug）。
- **改 XAML 前先跑 `python tools\check-xaml.py`。** XAML 注释里出现 `--` 报 WMC9997。
- **只暂存仓库文件**，绝不 `bin/`、`obj/`、`dist/`、`libs/`。

## Review Focus

spec 暗示、但没有任务的测试直接覆盖、且最容易咬到真实用户的五类：

1. **卡片回收时描边串台。** 滚动让同一个容器指向另一条曲目时，选中外框必须跟着新条目走，不能留着上一条的——这是 `ResetOverlay` 现有职责，本轮的选中保留描边是新的状态源。
2. **浅色模式与高对比度下的可见性。** 三个新令牌在浅色下有没有消失、在 HighContrast 下有没有变成看不见的同色。
3. **列表文件被外部改动。** 用户在资源管理器里删掉或编辑 `.m3u`，分区仍要能进、能显示剩下的。
4. **取消最后一个勾选。** 工具栏收起、勾选框回到悬浮才出现、卡片回退普通悬浮样式，三件事必须同时发生。
5. **列表名里的非法字符。** `\ / : * ? " < > |`、首尾空格、与已有列表重名。

---

## File Structure

| 文件 | 职责 | 动作 |
|---|---|---|
| `src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs` | 卡片几何常量：圆角、按钮、外框、新增网格间距 | 修改 |
| `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml` | 卡片视觉树：外框留槽、按钮缩小、勾选框 | 修改 |
| `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs` | 选中态与悬停态的状态机 | 修改 |
| `src/mpvw-app/Styles/Styles.xaml` | 三个新描边令牌 × 三套字典 | 修改 |
| `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml` | 网格默认布局、选择工具栏 | 修改 |
| `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Video.cs` | `CardLayout()` 间距 | 修改 |
| `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Music.cs` | `AlbumCardLayout()` 间距 | 修改 |
| `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs` | 选择模式状态机、分区路由 | 修改 |
| `src/mpvw-app/Domains/Playback/MpvPlayerPage.Playlists.cs` | 播放列表分区的行列表构建 | 新建 |
| `src/mpvw-app/Domains/Playlist/NamedPlaylistFile.cs` | `.m3u` 解析、格式化、名字校验（纯逻辑，可单测） | 新建 |
| `src/mpvw-app/Domains/Playlist/NamedPlaylistStore.cs` | 列表的读/写/改名/删除（IO + `AppData`） | 新建 |
| `src/mpvw-app/Shell/MainWindow.xaml.cs` | `ShellSections` 加回 `playlists` | 修改 |
| `tests/conf/CardMetricsTests.cs` | 几何断言跟着改 | 修改 |
| `tests/conf/NamedPlaylistFileTests.cs` | `.m3u` 解析/格式化 | 新建 |

---

### Task 0: Spike —— 外框超出格子会不会被裁

**Files:** 无（临时改动，验证后还原）

**Interfaces:**
- Consumes: 无
- Produces: 一个结论——Task 3 是否按「向外推 6 DIP」做，还是退回内缩方案

- [ ] **Step 1: 把外框的 Margin 改成负值**

改 `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml:82`：

```xml
Margin="-6"
```

- [ ] **Step 2: 构建并部署**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

Expected: 退出码 0，`differing: 0`。

- [ ] **Step 3: 启动并截图一张悬停的卡**

启动 `C:\Apps\mpvw\mpvw.exe`，进入视频库，鼠标停在**第一行最左**的卡上，截图。

Expected: 外框的四条边都可见，左侧和上方没有被网格边界切掉。

- [ ] **Step 4: 记录结论**

- 若外框完整可见 → Task 3 按 spec 的「向外推 6」执行。
- 若被裁 → 停止，向用户报告：外框必须改为内缩方案，spec 的 4.4 需要重写。**不要自行改成内缩。**

- [ ] **Step 5: 还原**

```bash
git checkout -- src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml
```

---

### Task 1: 三个描边令牌 × 三套主题字典

**Files:**
- Modify: `src/mpvw-app/Styles/Styles.xaml`（Light 段、Dark 段、HighContrast 段各加三条）
- Test: 无自动化（构建通过 + Task 2 的断言间接覆盖）

**Interfaces:**
- Consumes: 无
- Produces: `MpvCardFrameBrush`、`MpvCardButtonRingBrush`、`MpvCardCoverStrokeBrush`，类型均为 `SolidColorBrush`

- [ ] **Step 1: Light 字典内加三条**

`Styles.xaml` 的 `<ResourceDictionary x:Key="Light">` 段内（`MpvMusicArtworkFallbackBrush` 之后、`</ResourceDictionary>` 之前）加：

```xml
<SolidColorBrush x:Key="MpvCardCoverStrokeBrush" Color="#14000000" />
<SolidColorBrush x:Key="MpvCardFrameBrush" Color="#33000000" />
<SolidColorBrush x:Key="MpvCardButtonRingBrush" Color="#73000000" />
```

- [ ] **Step 2: Dark 字典内加三条**

`<ResourceDictionary x:Key="Dark">` 段内同样位置：

```xml
<SolidColorBrush x:Key="MpvCardCoverStrokeBrush" Color="#14FFFFFF" />
<SolidColorBrush x:Key="MpvCardFrameBrush" Color="#33FFFFFF" />
<SolidColorBrush x:Key="MpvCardButtonRingBrush" Color="#73FFFFFF" />
```

- [ ] **Step 3: HighContrast 字典内加三条**

`<ResourceDictionary x:Key="HighContrast">` 段内同样位置：

```xml
<SolidColorBrush x:Key="MpvCardCoverStrokeBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
<SolidColorBrush x:Key="MpvCardFrameBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
<SolidColorBrush x:Key="MpvCardButtonRingBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
```

- [ ] **Step 4: 构建**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
```

Expected: 0 错误 0 警告。

- [ ] **Step 5: Commit**

```bash
git add src/mpvw-app/Styles/Styles.xaml
git commit -m "Cards: three stroke tokens, one per theme, so a translucent edge can follow the theme"
```

---

### Task 2: 卡片几何常量

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs`
- Test: `tests/conf/CardMetricsTests.cs:62-90`（`TokensAreTheDocumentedValues`）

**Interfaces:**
- Consumes: 无
- Produces: `CardMetrics.CornerRadius = 6`、`HoverFrameCornerRadius = 12`、`HoverFrameInset = 6`、`PlayButtonSize = 30`、`MoreButtonSize = 30`、`CornerInset = 8`，新增 `GridColumnSpacing = 20`、`GridRowSpacing = 20`

- [ ] **Step 1: 改断言，先让它红**

`tests/conf/CardMetricsTests.cs` 的 `TokensAreTheDocumentedValues` 里：

```csharp
Assert.That(CardMetrics.CornerRadius, Is.EqualTo(6));
Assert.That(CardMetrics.PlayButtonSize, Is.EqualTo(30));
Assert.That(CardMetrics.MoreButtonSize, Is.EqualTo(30));
Assert.That(CardMetrics.CornerInset, Is.EqualTo(8));
Assert.That(CardMetrics.HoverFrameInset, Is.EqualTo(6));
Assert.That(CardMetrics.HoverFrameCornerRadius, Is.EqualTo(12));
Assert.That(CardMetrics.GridColumnSpacing, Is.EqualTo(20));
Assert.That(CardMetrics.GridRowSpacing, Is.EqualTo(20));
```

同文件加一条新测试：

```csharp
[Test]
public void HoverFrameRadiusIsConcentricWithTheContent()
{
    // The frame sits HoverFrameInset OUTSIDE the content, so its arc must be
    // that many DIP larger or the two edges are not parallel. This is the
    // relation the comparison page got wrong (it offered 11 against an
    // outward push of 6) and the reason the number is derived, not sampled.
    Assert.That(
        CardMetrics.HoverFrameCornerRadius,
        Is.EqualTo(CardMetrics.CornerRadius + CardMetrics.HoverFrameInset));
}
```

**`GridColumnSpacing` / `GridRowSpacing` 这一对先别加断言**——它们是 Task 5 才引入的常量，在这里写会让 Task 2 编译不过。把这两行留到 Task 5 的测试里。

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~CardMetricsTests"
```

Expected: FAIL，报 `CornerRadius` 期望 6 实际 4，且 `GridColumnSpacing` 不存在（编译错误也算红）。

- [ ] **Step 3: 改常量**

`CardMetrics.cs`：

```csharp
public const double CornerRadius = 6;
public const double HoverFrameInset = 6;
public const double HoverFrameCornerRadius = 12;
public const double PlayButtonSize = 30;
public const double MoreButtonSize = 30;
public const double CornerInset = 8;
```

`HoverFrameInset` 的 XML 注释改为描述「留槽宽度」：它是内容四周永久保留的 6 DIP，
外框与内容共用这一道槽（spike 实测：格子会裁掉画到外面的东西）。

`RingSitsOnTheDiscEdge`（`:98-102`）里的 `RingThickness < PlayButtonSize / 4` 在 30px 下是 `1.5 < 7.5`，仍然成立，不用改。

- [ ] **Step 4: 跑测试确认它绿**

同上命令。Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs tests/conf/CardMetricsTests.cs
git commit -m "Cards: bigger radii, smaller hover discs, and a frame radius derived from the outward push"
```

---

### Task 3: 卡片视觉树 —— 外框进留槽、按钮缩小、勾选框

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml`
- Test: `tests/conf/CardMetricsTests.cs:259-289`（`TheHoverFrameIsWhiteAndWrapsTheWholeCard`）

**Interfaces:**
- Consumes: Task 1 的三个令牌、Task 2 的常量
- Produces: 卡片外框用 `MpvCardFrameBrush`；按钮用 `MpvCardButtonRingBrush`

- [ ] **Step 1: 改断言，先让它红**

`TheHoverFrameIsWhiteAndWrapsTheWholeCard` 整个方法体替换为：

```csharp
[Test]
public void TheHoverFrameSitsInAGutterOutsideTheContent()
{
    var xaml = File.ReadAllText(CardXamlPath);

    var frame = SelfClosingElement(xaml, "HoverFrame");

    Assert.That(
        frame.Contains("{ThemeResource MpvCardFrameBrush}", StringComparison.Ordinal),
        "the frame must use the themed MpvCardFrameBrush, not the near-opaque " +
        "MpvCardRingBrush the buttons and the OSD capsule share");

    Assert.That(
        frame.Contains($"CornerRadius=\"{CardMetrics.HoverFrameCornerRadius}\"",
            StringComparison.Ordinal),
        "the frame's radius must be the content's plus the gutter, or the two arcs " +
        "are not concentric");

    // The gutter is permanent: the OUTER Grid carries it whether or not the
    // frame is showing, so the content never moves on hover. Measured: the grid
    // cell clips anything drawn outside it, so the frame lives in this gutter
    // rather than beyond the cell.
    var content = ContentGridFor(xaml);

    Assert.That(
        content.Contains($"Margin=\"{CardMetrics.HoverFrameInset}\"", StringComparison.Ordinal),
        $"the content must sit CardMetrics.HoverFrameInset ({CardMetrics.HoverFrameInset}) " +
        "inside the card at all times - a gutter that appears on hover would move the " +
        "picture, which is the one thing this card must not do");

    // And the frame must NOT be a sibling that stretches over the content: it is
    // the outermost layer, offset by the same gutter.
    Assert.That(
        frame.Contains("Margin=", StringComparison.Ordinal),
        Is.False,
        "the frame takes the gutter from its container, not from its own Margin - " +
        "two independent Margs are how the stroke and the content drift apart");
}
```

`ContentGridFor` 的查找串从 `"<Grid Margin="` 改为 `"<Grid x:Name=\"CardContent\""`（内容 Grid 即将获得这个名字），返回片段仍取到 `>` 为止。

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~TheHoverFrameSitsInAGutter"
```

Expected: FAIL —— 现在用的是 `MpvCardRingBrush`、`CornerRadius="8"`，外框自己带 `Margin="4"`，内容也带 `Margin="4"`。

- [ ] **Step 3: 改 XAML —— 让格子本身成为槽**

**注意：这一节最初的写法（包一层 `CardGutter`）是错的，实施时已被执行者用算术推翻。**
保留这段记录，是因为这个坑值得留着：先写下、再被推翻，比悄悄改掉更可信。

~~错在哪~~：若在根 Grid 里再包 `<Grid x:Name="CardGutter" Margin="6">`，把外框和无 Margin 的内容都放进去，
在 265 宽的格子里两者同框——描边正好压在内容边缘上，等于把本任务要修的毛病原样复现；
若再给内容加 `Margin="6"`，内缩叠加成 12，图片被缩到 241，违反「内容保持 253」。

**实际做法：根 Grid 就是格子，槽落在内容上。**

```xml
<!--  根 Grid 不加 Margin：它的宽度就是 CardMetrics.CellWidthFor(253) = 265  -->
<Border
    x:Name="HoverFrame"
    Canvas.ZIndex="10"
    BorderBrush="{ThemeResource MpvCardFrameBrush}"
    BorderThickness="1.5"
    CornerRadius="12"
    IsHitTestVisible="False"
    Visibility="Collapsed" />

<Grid
    x:Name="CardContent"
    Margin="6">
    <!-- 现有 StackPanel 与 HoverOverlay 原样留在里面  -->
</Grid>
```

要点：

- 外框**删掉自己的 `Margin="4"`，并且不获得新 Margin**——它在根 Grid 里 `Stretch`，占满 265
- 内容 `Grid` 从 `<Grid Margin="4">` 变成 `<Grid x:Name="CardContent" Margin="6">`：
  **整张卡唯一的一处内缩**，落在内容上
- 于是图片落在 6..259（253 宽），外框描边贴着格子内缘，与内容相距 6 DIP
- 整个卡片只有**一个** Margin。两个 Margin 正是描边与内容会慢慢错开的原因；
  测试断言外框那个 tag 不含 `Margin`

**已接受的副作用**：无。`Border` 把描边画在自己的框**内**，而外框的框就是格子，
所以 1.5 DIP 全程落在格子内，一个像素都没被裁。实测四条边的像素是满覆盖的
`(197,197,197)`（20% 黑压在 246 的悬浮底色上），没有半覆盖的边缘像素。

（这里原先写着「描边外侧 0.75 DIP 被格子裁掉」，是错的：裁的是**画到格子外面**的东西，
而 `Border` 从来不画到自己的框外面。Task 0 的探针测的正是画到格子外的情况，两者不是一回事。）

- [ ] **Step 4: 改卡片几何**

| 处 | 现在 | 改为 |
|---|---|---|
| `MediaBorder` 的 `CornerRadius` | `4` | `6` |
| `PlayButton` / `MoreButton` 的 `Width`/`Height` | `36` | `30` |
| 两颗按钮的 `Margin` | `10` | `8` |
| 两颗按钮的 `BorderBrush` | `{StaticResource MpvCardRingBrush}` | `{ThemeResource MpvCardButtonRingBrush}` |
| 两颗按钮的 `CornerRadius` | `18` | `15` |
| 两颗按钮的**字形** `Foreground` | `{StaticResource MpvCardRingBrush}` | **不动** |

`CardMetrics.cs` 加：

```csharp
/// <summary>
/// The card cell's width: the content plus the gutter on both sides.
///
/// The grid clips at the cell edge, so the frame cannot hang outside it. The
/// gutter is therefore part of the cell, and the CONTENT keeps the reference's
/// 253 - the picture must not shrink just because the frame needed a home.
/// </summary>
public static double CellWidthFor(double contentWidth) =>
    contentWidth + (HoverFrameInset * 2);
```

`CellHeightFor` 相应改为 **内容高度 + 上下槽 + 文字带**：

```csharp
public static double CellHeightFor(double contentWidth, bool isAlbum) =>
    CoverHeightFor(contentWidth, isAlbum) + (HoverFrameInset * 2) + CaptionBandHeight;
```

- [ ] **Step 5: 更新同文件里已经过时的注释**

外框上方那段「its 4 DIP margin is the gutter the stroke sits in」已经不成立（外框不再有 Margin），
改成描述外层 `CardGutter`。
内容 Grid 上方那段「inset by the frame's gutter so the stroke has room」同样改写：
槽在外层，内容本身不内缩。
按钮那段提到 `Margin="10"` / 36px / `CornerRadius="18"` 的字句一并改。

- [ ] **Step 6: 跑测试确认它绿**

```powershell
python tools\check-xaml.py
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~CardMetricsTests"
```

Expected: PASS，`check-xaml` 退出 0。

- [ ] **Step 7: Commit**

```bash
git add src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml src/mpvw-app/Domains/Playback/Cards/CardMetrics.cs tests/conf/CardMetricsTests.cs
git commit -m "Cards: the frame lives in a permanent gutter, because the grid cell clips what hangs outside it"
```

---

### Task 4: 常态封面描边

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml`（`MediaBorder`，`:96-99`）
- Test: `tests/conf/CardMetricsTests.cs`（新增一条）

**Interfaces:**
- Consumes: Task 1 的 `MpvCardCoverStrokeBrush`
- Produces: 无

- [ ] **Step 1: 写失败测试**

`CardMetricsTests.cs` 新增：

```csharp
/// <summary>
/// The cover wears a hairline in its resting state, and the caption does not.
///
/// The report asked for "a faint grey-white shadow stroke around the thumbnail"
/// while the card is not hovered. It is a hairline on the MEDIA BORDER, not a
/// halo around the whole card: the second would fight the hover frame, which is
/// the one that means "this card".
/// </summary>
[Test]
public void TheCoverWearsAHairlineAndTheCaptionDoesNot()
{
    var xaml = File.ReadAllText(CardXamlPath);

    var media = ElementFor(xaml, "MediaBorder");

    Assert.That(
        media.Contains("MpvCardCoverStrokeBrush", StringComparison.Ordinal),
        "MediaBorder must paint the resting-state hairline");

    Assert.That(
        TitleBlock(xaml).Contains("Stroke", StringComparison.Ordinal),
        Is.False,
        "the caption must not wear it - only the picture does");
}
```

`TitleBlock` 用文件里已有的 `ElementFor(xaml, "TitleText")` 代替，不必新增 helper。

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~TheCoverWearsAHairline"
```

Expected: FAIL —— `MediaBorder` 目前只有 `Background` 与 `CornerRadius`。

- [ ] **Step 3: 给 MediaBorder 加描边**

`LibraryCard.xaml` 的 `MediaBorder`（`:96-99`）加两个属性：

```xml
BorderBrush="{ThemeResource MpvCardCoverStrokeBrush}"
BorderThickness="1"
```

- [ ] **Step 4: 跑测试确认它绿**

同 Step 2 命令。Expected: PASS。

- [ ] **Step 5: 人眼验证**

构建部署后截图三张：常态、悬浮、已选中且鼠标移开。确认常态那圈描边**淡到不像边框**。

- [ ] **Step 6: Commit**

```bash
git add src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml tests/conf/CardMetricsTests.cs
git commit -m "Cards: a hairline on the cover, so a resting card is not a hole in the grid"
```

---

### Task 5: 网格间距改 20 —— 三处

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml:1332-1336`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Video.cs:342-348`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Music.cs:696-702`
- Test: `tests/conf/CardMetricsTests.cs`（新增一条）

**Interfaces:**
- Consumes: Task 2 的 `CardMetrics.GridColumnSpacing` / `GridRowSpacing`、Task 3 的 `CellWidthFor`
- Produces: 无

**间距的计算基准变了。** Task 0 的 spike 证明格子会裁掉画到外面的东西，所以 Task 3
把 6 DIP 槽做进了格子内部：卡片的 `MinItemWidth` 从 253 变成 `CellWidthFor(253)` = 265，
`MinItemHeight` 从 `CellHeightFor(253)` = 203.3 变成含上下槽的新值。
**内容（图片）仍是 253 宽**，变大的是格子。

- [ ] **Step 1: 写失败测试**

```csharp
/// <summary>
/// All three card grids must leave the frame a home.
///
/// The frame lives in a 6 DIP gutter INSIDE the cell (the grid clips anything
/// drawn outside it - measured, see Task 0), so the cell is wider than the
/// content, and the spacing is what keeps two selected frames apart. The grids
/// shipped 12, which put a selected card's frame 2 DIP from its neighbour's.
///
/// There are THREE layouts - two built in code, one in markup as the
/// first-paint default - and missing any of them leaves one screen tight.
///
/// The markup cannot name CardMetrics (it is internal static), so its numbers
/// are literals pinned by this test, the same arrangement LineHeight uses.
/// </summary>
[Test]
public void EveryCardGridLeavesRoomForTheFrame()
{
    Assert.That(CardMetrics.GridColumnSpacing, Is.EqualTo(20));
    Assert.That(CardMetrics.GridRowSpacing, Is.EqualTo(20));

    // Two selected frames, back to back, must not read as one line.
    Assert.That(
        CardMetrics.GridColumnSpacing,
        Is.GreaterThan(CardMetrics.HoverFrameInset * 2));

    var page = File.ReadAllText(PageXamlPath);

    Assert.That(
        page.Contains($"MinColumnSpacing=\"{CardMetrics.GridColumnSpacing}\"", StringComparison.Ordinal),
        "the markup's default grid must use the same column spacing");

    Assert.That(
        page.Contains($"MinRowSpacing=\"{CardMetrics.GridRowSpacing}\"", StringComparison.Ordinal),
        "the markup's default grid must use the same row spacing");

    // The cell is the content plus the gutter on both sides.
    const double ContentWidth = 253;

    Assert.That(
        page.Contains($"MinItemWidth=\"{CardMetrics.CellWidthFor(ContentWidth)}\"", StringComparison.Ordinal),
        "the markup's cell must be wide enough for the content AND the frame's gutter");

    var minItemHeight = System.Text.RegularExpressions.Regex.Match(
        page, @"MinItemHeight=""([\d.]+)""");

    Assert.That(minItemHeight.Success, "no MinItemHeight in the markup grid");

    Assert.That(
        double.Parse(minItemHeight.Groups[1].Value, CultureInfo.InvariantCulture),
        Is.GreaterThanOrEqualTo(CardMetrics.CellHeightFor(ContentWidth, isAlbum: false)),
        "the first-paint cell is shorter than CardMetrics says the card needs, so the " +
        "caption band is clipped until ApplyLibraryGridLayout replaces it");
}
```

`PageXamlPath` 按 `CardXamlPath`（`:479-505`）同样的方式解析 `src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml`。

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~EveryCardGridLeavesRoom"
```

Expected: FAIL —— markup 用的是 `{StaticResource MpvSpaceM}`（12）、`MinItemWidth="253"`（应为 265）、`MinItemHeight="192"`。

- [ ] **Step 3: 改三处布局 + 加两个常量**

`CardMetrics.cs` 加：

```csharp
/// <summary>The card grids' spacing, in DIP. Shared with the markup's literal.</summary>
public const double GridColumnSpacing = 20;

public const double GridRowSpacing = 20;
```

markup（`MpvPlayerPage.xaml:1332-1336`）：

```xml
<UniformGridLayout
    MinColumnSpacing="20"
    MinItemHeight="216"
    MinItemWidth="265"
    MinRowSpacing="20" />
```

`CardLayout()`（`MpvPlayerPage.Library.Video.cs:342-348`）：

```csharp
// The cell is the content plus the frame's gutter on both sides.
const double width = 253;

MinItemWidth = CardMetrics.CellWidthFor(width),
MinItemHeight = CardMetrics.CellHeightFor(width, isAlbum: false),
MinColumnSpacing = CardMetrics.GridColumnSpacing,
MinRowSpacing = CardMetrics.GridRowSpacing,
```

`AlbumCardLayout()`（`MpvPlayerPage.Library.Music.cs:696-702`）：同样处理，`width` 保留 170，
`MinItemWidth = CardMetrics.CellWidthFor(width)`，行距改走 `CardMetrics.GridRowSpacing`。

同时在 markup 那段注释里加一句：`MinItemWidth="265"` 与 `MinItemHeight="216"` 是
`CellWidthFor(253)` 与 `CellHeightFor(253)` 的结果，由 `CardMetricsTests` 钉住，**不要**改回 253/192。

- [ ] **Step 4: 跑测试确认它绿**

Step 2 命令。Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Video.cs src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Music.cs tests/conf/CardMetricsTests.cs
git commit -m "Cards: the cell grows by the gutter, and every grid opens 20 DIP"
```

---

### Task 6: 选中态保留外框

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs`
- Test: 无自动化（时序与视觉，靠 Task 7 的 UI 自动化间接覆盖）

**Interfaces:**
- Consumes: Task 3 的外框
- Produces: `LibraryCard` 在 `Entry.IsSelected` 为真且指针不在卡上时，`HoverFrame.Visibility == Visible`

- [ ] **Step 1: 让 `ApplySelection` 驱动外框**

`LibraryCard.xaml.cs` 的 `ApplySelection(entry)` 末尾加一句：

```csharp
// A ticked card keeps its frame after the pointer leaves: the frame IS the
// "this one" mark, and a tick box alone disappears under the cursor.
UpdateFrameForState();
```

- [ ] **Step 2: 加 `UpdateFrameForState`**

```csharp
/// <summary>
/// The frame is on when the pointer is on the card OR the card is ticked.
///
/// Both callers - the pointer and the entry's IsSelected - go through here, so
/// there is one place that decides. Two independent writers of the same
/// Visibility is how a card ends up wearing another card's highlight after a
/// recycle, which is the defect ResetOverlay already exists to prevent.
/// </summary>
private void UpdateFrameForState()
{
    var selected = Entry?.IsSelected == true;

    HoverFrame.Visibility = _pointerInside || selected
        ? Visibility.Visible
        : Visibility.Collapsed;
}
```

- [ ] **Step 3: 用 `_pointerInside` 取代直接写 Visibility**

加字段 `private bool _pointerInside;`。把 `ShowOverlay()` / `HideOverlay()` / `ResetOverlay()` 里所有 `HoverFrame.Visibility = ...` 替换为设置 `_pointerInside` 后调用 `UpdateFrameForState()`。`HideOverlay()` 里那句「Unconditionally off」的注释随之改写。

- [ ] **Step 4: `IsSelected` 变化时重算**

`OnEntryPropertyChanged` 的 `case nameof(LibraryEntry.IsSelected):` 分支里，在 `ApplyCaptionEmphasis(entry);` 之后加 `UpdateFrameForState();`。

- [ ] **Step 5: 构建并人眼验证**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

勾选一张卡，把鼠标移到别处，截图：外框还在。再取消勾选，外框随之消失。

- [ ] **Step 6: Commit**

```bash
git add src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs
git commit -m "Cards: a ticked card keeps its frame when the pointer leaves"
```

---

### Task 7: 选择模式 ≡ 有勾选

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs:547-609`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs`（`CardMenu_Click` 的 `"select"` 分支、卡片点击）
- Test: `tests/conf/CardMetricsTests.cs`（新增一条，断言状态机不引入独立开关）

**Interfaces:**
- Consumes: 无
- Produces: `UpdateSelectionBar()` 是唯一的模式推进点；`_selectionMode` 只被它写

- [ ] **Step 1: 写失败测试**

选择模式的真值由选中数决定，这一点在没有 WinUI 的测试项目里只能靠读源码钉住——把它写成一条源码断言，与仓库里其它「这条约束没有别的办法观察」的做法一致：

```csharp
/// <summary>
/// The selection mode is the selection, not a switch beside it.
///
/// Reported twice, in this order: "0 selected means the bar is gone and the
/// cards go back to their normal hover", then "with one selected the others
/// show their boxes too". Both hold only if unticking the last card leaves the
/// mode - otherwise the mode is on with nothing on screen to say so, and the
/// only way out is a menu item the user has no reason to look for.
///
/// Asserted from the source because the state lives behind WinUI types the
/// test project cannot reference, and a comment saying "mode follows the count"
/// is exactly the kind of thing that stops being true.
/// </summary>
[Test]
public void SelectionModeFollowsTheCountRatherThanASwitch()
{
    var source = File.ReadAllText(PageLibrarySourcePath);

    var body = MethodBody(source, "UpdateSelectionBar");

    Assert.That(
        body.Contains("_selectionMode =", StringComparison.Ordinal),
        "UpdateSelectionBar must be the place the mode is decided");

    // No other method may assign the flag: SetSelectionMode is gone.
    Assert.That(
        source.Contains("private void SetSelectionMode", StringComparison.Ordinal),
        Is.False,
        "there must be no SetSelectionMode(bool): a public on/off is what let the mode " +
        "outlive the ticks");
}
```

`PageLibrarySourcePath` 按 `CardXamlPath` 的方式解析 `src\mpvw-app\Domains\Playback\MpvPlayerPage_Library.cs`。`MethodBody(source, name)` 截取从 `name` 首次出现的 `{` 到配对 `}` 的文本。

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~SelectionModeFollowsTheCount"
```

Expected: FAIL —— `SetSelectionMode` 存在。

- [ ] **Step 3: 重写状态推进**

`UpdateSelectionBar()` 改为：

```csharp
private void UpdateSelectionBar()
{
    var selected = _visibleEntries.Count(entry => entry.IsSelected);

    // The mode IS the count. Deciding it here - and only here - is what keeps
    // "one ticked means every card shows its box" and "none ticked means the
    // bar is gone" from being two rules that can disagree.
    var mode = selected > 0;

    if (mode != _selectionMode)
    {
        _selectionMode = mode;

        foreach (var entry in _visibleEntries)
        {
            entry.SetSelectionMode(mode);
        }
    }

    LibrarySelectionBar.Visibility = mode ? Visibility.Visible : Visibility.Collapsed;

    LibrarySelectionCount.Text = string.Format(
        AppContext.AppLang.LibrarySelectionCount, selected);

    var hasSelection = selected > 0;

    SelectionPlayButton.IsEnabled = hasSelection;
    SelectionQueueButton.IsEnabled = hasSelection;
    SelectionPropertiesButton.IsEnabled = selected == 1;
}
```

- [ ] **Step 4: 删掉 `SetSelectionMode`，改调用点**

- 删掉 `private void SetSelectionMode(bool on)` 整个方法。
- `SelectionClear_Click` 改为：先把所有 `IsSelected` 置 false，再调 `UpdateSelectionBar()`。
- `MpvPlayerPage.Library.Cards.cs` 的 `"select"` 分支改为只 `entry.SetSelected(true); UpdateSelectionBar();`（不再 `SetSelectionMode(true)`）。
- 卡片点击的分支（`if (_selectionMode) { entry.ToggleSelection(); UpdateSelectionBar(); }`）不变。

- [ ] **Step 5: 提升所有与模式相关的注释**

`MpvPlayerPage_Library.cs:534-545` 那段 doc 注释现在说「both the flag and the visible bar move together」，改为描述「mode 由计数推出」。`:604-609` 关于「Leaving clears every tick」的注释同样改写：现在没有独立的「离开」，清空勾选就是离开。

- [ ] **Step 6: 跑测试确认它绿**

Step 2 命令 + 完整测试套件：

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests
```

Expected: PASS，总数比基线多出本轮新增的条数。

- [ ] **Step 7: Commit**

```bash
git add src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs tests/conf/CardMetricsTests.cs
git commit -m "Selection: the mode is the count of ticks, so unticking the last one leaves it"
```

---

### Task 8: 勾选框出现在卡片左上，且是模式入口

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml`（`SelectionBox`，`:348-366`）
- Modify: `src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs`
- Test: `tests/conf/CardMetricsTests.cs`（新增一条）

**Interfaces:**
- Consumes: Task 7 的状态机
- Produces: `LibraryCard` 暴露 `public event EventHandler? SelectionRequested`——勾选框被点时触发

- [ ] **Step 1: 写失败测试**

```csharp
/// <summary>
/// The tick box is an affordance, not a readout, so it takes clicks.
///
/// It used to live inside HoverOverlay with IsHitTestVisible="False" and no
/// handler: it showed a state and could not change one, which is why the only
/// way into selection mode was a context menu item nobody would find. It also
/// sat on the tile's top-left and vanished with the overlay, so a ticked card
/// lost its mark as soon as the pointer left.
/// </summary>
[Test]
public void TheTickBoxIsClickableAndOutlivesTheOverlay()
{
    var xaml = File.ReadAllText(CardXamlPath);

    var box = ElementFor(xaml, "SelectionBox");

    Assert.That(
        box.Contains("Tapped=", StringComparison.Ordinal)
            || box.Contains("Click=", StringComparison.Ordinal),
        "the tick box must have a handler: it is the way into selection mode");

    Assert.That(
        box.Contains("IsHitTestVisible=\"False\"", StringComparison.Ordinal),
        Is.False,
        "a hit-test-invisible tick box is a readout, not an entry point");

    // It is a sibling of the overlay now, not a child: the overlay collapses
    // on pointer exit, and a tick mark that disappears with it is not a mark.
    var overlayEnd = xaml.IndexOf("</Grid>", xaml.IndexOf("x:Name=\"HoverOverlay\"", StringComparison.Ordinal), StringComparison.Ordinal);
    var boxAt = xaml.IndexOf("x:Name=\"SelectionBox\"", StringComparison.Ordinal);

    Assert.That(
        boxAt, Is.GreaterThan(overlayEnd),
        "SelectionBox must sit outside HoverOverlay; inside it, it collapses with the hover");
}
```

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~TheTickBoxIsClickable"
```

Expected: FAIL —— 现在没有 handler，且由外层 `Grid IsHitTestVisible="False"` 覆盖。

- [ ] **Step 3: 把勾选框移出 HoverOverlay 并让它可点**

`LibraryCard.xaml`：把 `SelectionBox`（`:348-366`）从 `HoverOverlay` 内移到卡片根 `Grid` 里、作为 `HoverOverlay` 之后的兄弟，改成一个 `Button`：

```xml
<Button
    x:Name="SelectionBox"
    Width="24"
    Height="24"
    Margin="8"
    Padding="0"
    HorizontalAlignment="Left"
    HorizontalContentAlignment="Center"
    VerticalAlignment="Top"
    VerticalContentAlignment="Center"
    Background="{ThemeResource ControlFillColorDefaultBrush}"
    BorderBrush="{ThemeResource MpvCardButtonRingBrush}"
    BorderThickness="1"
    Click="SelectionBox_Click"
    CornerRadius="6"
    Visibility="Collapsed">
    <FontIcon
        x:Name="SelectionGlyphIcon"
        FontSize="{StaticResource MpvCardTickFontSize}" />
</Button>
```

`Margin="8"` 落在画面本体的左上角内（与按钮内缩 8 一致）。

注意 `SelectionBox` 从 `Border` 变成 `Button`：`ApplySelection` 里现在写的
`SelectionBox.Visibility = entry.SelectionVisibility;` 仍然有效（两者都有 `Visibility`），
但类型从 `Border` 变成 `Button`，字段名不变、用法不变。

- [ ] **Step 4: 代码侧**

`LibraryCard.xaml.cs` 加：

```csharp
/// <summary>Raised when the tick box is clicked. The page owns the mode.</summary>
public event EventHandler? SelectionRequested;

private void SelectionBox_Click(object sender, RoutedEventArgs e)
{
    e.Handled = true;

    SelectionRequested?.Invoke(this, EventArgs.Empty);
}
```

`ApplySelection(entry)` 里原来只设 `SelectionBox.Visibility = entry.SelectionVisibility`，保持；但要让它与 `HoverOverlay` 的显隐解耦——`SelectionBox` 现在是兄弟，`ResetOverlay` 不再影响它。

`SetEntry` 时要退订上一篇的 `SelectionRequested`（页面在 `ElementPrepared` 里挂的那个），避免回收后重复触发。

- [ ] **Step 5: 页面侧接上**

`MpvPlayerPage.Library.Cards.cs` 的 `LibraryGrid_ElementPrepared` 里，给每张卡挂：

```csharp
card.SelectionRequested -= Card_SelectionRequested;
card.SelectionRequested += Card_SelectionRequested;
```

处理器：

```csharp
private void Card_SelectionRequested(object? sender, EventArgs e)
{
    if (sender is not LibraryCard { Entry: { } entry })
    {
        return;
    }

    entry.ToggleSelection();

    UpdateSelectionBar();
}
```

**`LibraryEntry.ToggleSelection` 的守卫必须去掉。** 它现在要求 `_selectionMode` 为真才翻转（`LibraryEntry.Selection.cs:88-96`），
而 Task 7 之后 `_selectionMode` 只在**已有选中**时才为真——于是第一次点勾选框永远勾不上任何东西。
这是 Task 7 与 Task 8 之间的一处真实冲突，裁定为：删掉那个 `if (!_selectionMode) return;`，
改为无条件翻转，并改写它的 doc 注释（模式现在由计数定义，所以「不在模式里」这个状态不存在）。

- [ ] **Step 6: 跑测试 + 构建**

```powershell
python tools\check-xaml.py
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests
.\build.ps1 -Configuration Release -Platform x64
```

Expected: 全绿，0 警告。

- [ ] **Step 7: 人眼验证**

- 未选中任何卡：鼠标移上一张卡 → 左上出现勾选框，点它 → 工具栏出现，**所有**卡的勾选框同时出现。
- 再点一次同一张卡 → 工具栏消失，所有勾选框回到悬浮才显示。

- [ ] **Step 8: Commit**

```bash
git add src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml src/mpvw-app/Domains/Playback/Controls/LibraryCard.xaml.cs src/mpvw-app/Domains/Playback/MpvPlayerPage.Library.Cards.cs src/mpvw-app/Domains/Playback/LibraryEntry.Selection.cs tests/conf/CardMetricsTests.cs
git commit -m "Cards: the tick box is the way into selection mode, and it survives the pointer leaving"
```

---

### Task 9: 选择工具栏按截图重做

**Files:**
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml:1204-1295`（`LibrarySelectionBar`）
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（按钮状态、字符串）
- Test: 无自动化（视觉，靠截图）

**Interfaces:**
- Consumes: Task 8 的可见性
- Produces: 工具栏新增 `SelectionAddToButton`（`DropDownButton`）与 `SelectionMoreButton`

- [ ] **Step 1: 按截图排按钮**

工具栏从左到右：`✓ 已选择 N 项 · 清除`（左），右侧依次 `播放`（Accent）、`加入播放队列`、`队列`、`添加到`（DropDownButton）、`更多`（DropDownButton）。现有 `SelectionPropertiesButton`（「在文件夹中显示」「属性」）移进 `更多` 的菜单。

- [ ] **Step 3: 「添加到」的菜单在打开时构建**

**本任务不引用 Task 12 才创建的东西。** `_namedPlaylists` 在这里声明成空列表，
`PlaylistCreate` 这个语言键在这里加（`AppLang.Shell.cs` + zh-CN 写「新建播放列表…」，
其余八份填中文占位——沿用设置页那轮的做法）。Task 12 再把空列表换成 store 的查询结果。

`MpvPlayerPage_Library.cs` 里加：

```csharp
/// <summary>Named playlists, for the add-to menu. Empty until Task 12 fills it.</summary>
private IReadOnlyList<NamedPlaylist> _namedPlaylists = [];
```

`SelectionAddToButton` 的 `Flyout` 用 `MenuFlyout`，在 `Opening` 时清空并重建：

```csharp
private void SelectionAddTo_Opening(object? sender, object e)
{
    var menu = SelectionAddToButton.Flyout as MenuFlyout;
    if (menu is null) { return; }

    menu.Items.Clear();

    var queue = new MenuFlyoutItem { Text = AppContext.AppLang.LibraryQueueAddTo };
    queue.Click += SelectionAddToQueue_Click;
    menu.Items.Add(queue);

    menu.Items.Add(new MenuFlyoutSeparator());

    // Named lists, in name order. Empty until Task 12 lands, which is why the
    // screenshot shows exactly two items: at that moment there were none.
    foreach (var playlist in _namedPlaylists)
    {
        var item = new MenuFlyoutItem { Text = playlist.Name };
        item.Click += (_, _) => AddSelectionToPlaylist(playlist.Name);
        menu.Items.Add(item);
    }

    menu.Items.Add(new MenuFlyoutSeparator());

    var create = new MenuFlyoutItem { Text = AppContext.AppLang.PlaylistCreate };
    create.Click += SelectionAddToNewPlaylist_Click;
    menu.Items.Add(create);
}
```

- [ ] **Step 3: 构建并截图**

```powershell
python tools\check-xaml.py
.\build.ps1 -Configuration Release -Platform x64
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

截图：工具栏全貌，外加「添加到」展开。与用户提供的参考截图逐项对照。

- [ ] **Step 4: Commit**

```bash
git add src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs
git commit -m "Selection: the bar from the reference, with the add-to list built when it opens"
```

---

### Task 10: `.m3u` 解析与格式化（纯逻辑）

**Files:**
- Create: `src/mpvw-app/Domains/Playlist/NamedPlaylistFile.cs`
- Modify: `tests/conf/mpv-conf-test.csproj`（加 `Compile Include` 链接）
- Test: `tests/conf/NamedPlaylistFileTests.cs`

**Interfaces:**
- Consumes: 无（不引用 WinUI，不引用 `Windows.Storage`）
- Produces:
  - `static IReadOnlyList<string> NamedPlaylistFile.ParsePaths(string content)` —— 返回条目路径，按文件顺序
  - `static string NamedPlaylistFile.Format(IReadOnlyList<(string Title, string Path)> items)` —— 生成带 `#EXTM3U` 的文本
  - `static bool NamedPlaylistFile.IsValidName(string name, out string? reason)` —— 名字合法性

**测试工程必须显式链接这个文件。** `tests/conf/mpv-conf-test.csproj` 现在**没有**引用
`Domains/Playlist/**`，所以新文件不会自动进测试编译——测试会以「类型不存在」失败，
看着像实现写错了。照 csproj 里 `CardMetrics` 那条的写法加：

```xml
<!-- The named-playlist m3u. Linked for the usual reason: the property that
     matters is which inputs are accepted and which lines become paths, and
     both are pure functions. -->
<Compile Include="..\..\src\mpvw-app\Domains\Playlist\NamedPlaylistFile.cs" Link="Playlist\NamedPlaylistFile.cs" />
```

**只链 `NamedPlaylistFile.cs`，不要链整个 `Domains/Playlist/**`**：那个目录下的
`WatchLaterControl.xaml.cs` 等文件引用 WinUI 类型，链进来会让整个测试工程编译不过。

- [ ] **Step 1: 写失败测试**

`tests/conf/NamedPlaylistFileTests.cs`（新建）。先按上面 Files 段加 csproj 链接，否则下面会是编译错误而不是断言失败。

```csharp
[TestFixture]
public class NamedPlaylistFileTests
{
    [Test]
    public void ParseReadsBarePathsAndExtinf()
    {
        var content = string.Join("\n",
            "#EXTM3U",
            "#EXTINF:-1,First",
            @"C:\media\a.mp4",
            @"C:\media\b.mp4");

        Assert.That(
            NamedPlaylistFile.ParsePaths(content),
            Is.EqualTo(new[] { @"C:\media\a.mp4", @"C:\media\b.mp4" }));
    }

    [Test]
    public void ParseSkipsCommentsAndBlankLines()
    {
        var content = "#EXTM3U\n\n# a comment\nC:\\a.mp4\n\n";

        Assert.That(NamedPlaylistFile.ParsePaths(content), Is.EqualTo(new[] { @"C:\a.mp4" }));
    }

    [Test]
    public void ParseOfGarbageYieldsNothingRatherThanThrowing()
    {
        Assert.That(NamedPlaylistFile.ParsePaths("\u0000\u0001 not a playlist"), Is.Empty);
    }

    [Test]
    public void FormatSurvivesATitleWithLineBreaks()
    {
        var text = NamedPlaylistFile.Format([("A\r\nB", @"C:\x.mp4")]);

        Assert.That(text, Does.Contain("A B"));
        Assert.That(text.Split('\n').Length, Is.EqualTo(3));
    }

    [Test]
    public void NamesWithPathCharactersAreRejected()
    {
        foreach (var bad in new[] { "a/b", "a\\b", "a:b", "a*b", "a?b", "a\"b", "a<b", "a>b", "a|b" })
        {
            Assert.That(
                NamedPlaylistFile.IsValidName(bad, out var reason), Is.False,
                $"{bad} must be refused - it is a file name");
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void EmptyOrWhitespaceNamesAreRejected()
    {
        Assert.That(NamedPlaylistFile.IsValidName("", out _), Is.False);
        Assert.That(NamedPlaylistFile.IsValidName("   ", out _), Is.False);
        Assert.That(NamedPlaylistFile.IsValidName("  padded  ", out _), Is.False);
    }

    [Test]
    public void AnOrdinaryNameIsAccepted()
    {
        Assert.That(NamedPlaylistFile.IsValidName("2026 秋", out var reason), Is.True);
        Assert.That(reason, Is.Null);
    }
}
```

- [ ] **Step 2: 跑测试确认它红**

```powershell
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests --filter "FullyQualifiedName~NamedPlaylistFileTests"
```

Expected: FAIL，`NamedPlaylistFile` 不存在（编译错误）。

- [ ] **Step 3: 实现**

`NamedPlaylistFile.cs`：三个静态方法，文件头注释写明它**故意不引用 WinUI 与 `Windows.Storage`**，这样 `tests/conf`（plain net10.0）能直接测——与 `CardMetrics` 同样的取舍。

`ParsePaths`：逐行 `Trim()`；跳过空行、以 `#` 开头的行；其余整行即路径。
`Format`：写 `#EXTM3U`，每条先写 `#EXTINF:-1,<title 去掉换行>`，再写路径。
`IsValidName`：拒绝空、首尾有空白、含 `Path.GetInvalidFileNameChars()` 的任一字符；`reason` 返回该字符或原因，合法时 `null`。

- [ ] **Step 4: 跑测试确认它绿**

Step 2 命令。Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/mpvw-app/Domains/Playlist/NamedPlaylistFile.cs tests/conf/NamedPlaylistFileTests.cs
git commit -m "Playlists: the m3u the app already writes and reads, as a pure pair of functions"
```

---

### Task 11: 列表的读 / 写 / 改名 / 删除

**Files:**
- Create: `src/mpvw-app/Domains/Playlist/NamedPlaylistStore.cs`
- Test: 无自动化（IO 与 `AppData`，靠 Task 12 的手动验证）

**Interfaces:**
- Consumes: Task 10 的 `NamedPlaylistFile`
- Produces:
  - `sealed class NamedPlaylistStore`，构造无参
  - `IReadOnlyList<NamedPlaylist> List()` —— `NamedPlaylist` 有 `string Name`、`IReadOnlyList<string> Paths`、`int Count`
  - `void Append(string name, IEnumerable<string> paths)` —— 已存在则追加并去重
  - `void Create(string name, IEnumerable<string> paths)`
  - `void Rename(string from, string to)`
  - `void Delete(string name)`
  - 所有方法在 IO 失败时记日志并返回，不抛

- [ ] **Step 1: 实现**

目录：`AppData.Current.ResolveLocalData("playlists")`，构造时 `Directory.CreateDirectory`。
文件名：`<name>.m3u`。写入用 `AtomicFile.WriteAllText`（`Infrastructure/Common/Utils/AtomicFile.cs:43`）。
`List()` 枚举 `*.m3u`，用 `NamedPlaylistFile.ParsePaths` 读条目，按名称排序返回；单个文件读失败记日志后跳过，不影响其它。

- [ ] **Step 2: 构建**

```powershell
.\build.ps1 -Configuration Release -Platform x64
```

Expected: 0 错误 0 警告。

- [ ] **Step 3: Commit**

```bash
git add src/mpvw-app/Domains/Playlist/NamedPlaylistStore.cs
git commit -m "Playlists: one m3u per list under the app's own data root"
```

---

### Task 12: 侧栏分区与行列表

**Files:**
- Modify: `src/mpvw-app/Shell/MainWindow.xaml.cs:186-198`
- Create: `src/mpvw-app/Domains/Playback/MpvPlayerPage.Playlists.cs`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml`（新增 `PlaylistsHost`）
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（分区路由、空态）
- Test: 无自动化（靠 UI 自动化与手动）

**Interfaces:**
- Consumes: Task 11 的 `NamedPlaylistStore`
- Produces: `MpvPlayerPage` 上的 `_namedPlaylists` 字段，Task 9 的「添加到」读它

- [ ] **Step 1: 侧栏加回**

`Shell/MainWindow.xaml.cs:186-198` 的 `ShellSections` 数组加一项：

```csharp
new("playlists", "\uE8FD", static l => l.ShellNavPlaylists),
```

并把上方那段「why playlists was removed」的注释改写：那次删的是 mpv 队列的第三份重复视图；命名播放列表是不同的东西，有自己的一份内容，所以回来了。图标 `\uE8FD` 是 Fluent 的 `Library`。

- [ ] **Step 2: 分区路由**

`MpvPlayerPage_Library.cs` 的 `ApplyEmptyStateContent`（`:394-436`）里，`LibrarySectionPlaylists` 的分支改为：有列表时不显示空态；无列表时保留「你没有任何播放列表」与「播放列表会显示在这里。」，且**加上**一个「新建播放列表」按钮（现在那里刻意不给按钮，理由注释要改）。

`ApplySectionHeader`（`:284-330`）里 `PlaylistsHost` 只在该分区可见。

- [ ] **Step 3: 行列表**

`MpvPlayerPage.Playlists.cs`：`PlaylistsHost` 是一个 `StackPanel`，`BuildPlaylistRows()` 清空后为每个列表加一行——行内容为名称、`N 项`、播放按钮、更多按钮（`MenuFlyout`：播放 / 重命名 / 删除）。

播放 = `_mediaPlayer.Command(["osd-auto", "loadlist", <m3u 路径>])`，照 `MpvPlayerPage_Playlist.cs:279` 的写法。
删除走 `ContentDialog` 确认，照 `MpvPlayerPage_History.cs:34-47` 的写法。

- [ ] **Step 4: 语言键**

九份 `Languages/*.json` 各加：`PlaylistCreate`、`PlaylistRename`、`PlaylistDelete`、`PlaylistDeleteConfirm`、`PlaylistNamePlaceholder`、`PlaylistItemCount`。`zh-CN` 写正确中文，其余八份填中文占位（沿用设置页那轮的做法）。同步在 `AppLang.Shell.cs` 加对应属性——`check-localization.py` 会核对两侧。

- [ ] **Step 5: 构建 + 检查器**

```powershell
python tools\check-xaml.py
python tools\check-localization.py
.\build.ps1 -Configuration Release -Platform x64
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

Expected: 全部退出 0。

- [ ] **Step 6: 人眼验证**

- 侧栏出现「播放列表」。
- 无列表时是空态与「新建」按钮。
- 手工在 `%LOCALAPPDATA%\mpv-winui\mpv-winui\playlists` 放一个 `.m3u`，重进分区能看到它、条目数正确、点播放能放。
- 删掉那个文件，重进分区只剩空态——不崩。

- [ ] **Step 7: Commit**

```bash
git add src/mpvw-app/Shell/MainWindow.xaml.cs src/mpvw-app/Domains/Playback/MpvPlayerPage.Playlists.cs src/mpvw-app/Domains/Playback/MpvPlayerPage.xaml src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs src/mpvw-app/Languages src/mpvw-app/Shell/AppLang.Shell.cs
git commit -m "Playlists: a section of their own again, as rows rather than cards"
```

---

### Task 13: 命名对话框与「新建播放列表」

**Files:**
- Create: `src/mpvw-app/Domains/Playback/MpvPlayerPage.PlaylistDialog.cs`
- Modify: `src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs`（`SelectionAddToNewPlaylist_Click`）
- Test: 无自动化（对话框，靠手动）

**Interfaces:**
- Consumes: Task 10 的 `IsValidName`、Task 11 的 `Create` / `Append`
- Produces: `private Task<string?> PromptForPlaylistNameAsync()` —— 返回合法名字或 `null`

- [ ] **Step 1: 实现对话框**

`ContentDialog`，内容是一个 `TextBox` 与一行错误提示。主按钮在名字非法时禁用；非法原因来自 `NamedPlaylistFile.IsValidName`，另外再加一条：与已有列表重名时提示改名，但仍允许（等于往里加）。

- [ ] **Step 2: 接上两个入口**

- 选择工具栏的「添加到 → 新建播放列表…」：`PromptForPlaylistNameAsync()` 之后 `Create(name, selectedPaths)`，再 `_namedPlaylists` 重读并 `UpdateSelectionBar()`。
- 播放列表分区空态的那个按钮：同样开对话框，建一个空列表。

- [ ] **Step 3: 构建 + 手动验证**

```powershell
.\build.ps1 -Configuration Release -Platform x64
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

依次试：空名字（主按钮禁用）、`a/b`（提示非法字符）、`  padded  `（提示首尾空格）、正常名字（建成并出现在「添加到」清单与播放列表分区）。

- [ ] **Step 4: Commit**

```bash
git add src/mpvw-app/Domains/Playback/MpvPlayerPage.PlaylistDialog.cs src/mpvw-app/Domains/Playback/MpvPlayerPage_Library.cs
git commit -m "Playlists: a name is checked before it becomes a file name"
```

---

### Task 14: 收尾 —— 检查器、绑定线、文档

**Files:**
- Modify: `tools/typography-baseline.txt`、`tools/icon-standard-baseline.txt`（若行号位移）
- Modify: `docs/` 下与卡片令牌相关的记录（若有）

**Interfaces:**
- Consumes: 前面全部
- Produces: 一次可交付的绿色基线

- [ ] **Step 1: 跑完整检查器清单**

按 `docs/verification.md` 的清单跑。特别关注：

```powershell
python tools\check-xaml.py
python tools\check-resources.py
python tools\check-scroll-maxwidth.py
python tools\check-name-collisions.py
python tools\check-typography-standard.py
python tools\check-icon-standard.py
python tools\check-visual-state-layout.py
python tools\check-localization.py
```

Expected: 除 `check-doc-ranges` 的 2 条**既有**失败外，全部退出 0。

- [ ] **Step 2: 基线位移**

若 Step 1 报基线行号不匹配：

```powershell
python tools\check-typography-standard.py --update-baseline
```

**跑完必须手工补回被丢掉的手写注释**——`--update-baseline` 会丢注释，这是 `AGENTS.md` 记过的坑。

- [ ] **Step 3: 全量构建与测试**

```powershell
.\build.ps1 -Configuration Release -Platform x64
dotnet test tests\conf\mpv-conf-test.csproj -c Release --artifacts-path C:\Temp\mpvw-tests
$env:MPVW_DEPLOY_DIR = "C:\Apps\mpvw"
pwsh -NoProfile -File tools\deploy-test.ps1 -CloseRunning -Verify
```

Expected: 0 错误 0 警告；测试全绿且总数 ≥ 557 + 本轮新增；`differing: 0`。

- [ ] **Step 4: UI 自动化的 cards board**

```powershell
python tools/ui-design-audit.py --board cards
```

Expected: 退出 0。**先注入一次真实违规确认它会红**（例如把 `HoverFrame` 的 `Margin` 改回 `4`），再还原转绿。

- [ ] **Step 5: 把本轮推翻的旧事实记进 docs**

`docs/` 里若有文字说「卡片令牌只有单一值」或「播放列表分区已移除」，改掉。按 `docs/README.md` 的规矩：文档与代码冲突以代码为准，并把冲突记回文档。改完跑 `check-doc-freshness` / `check-doc-references` / `check-doc-tooling`。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Cards and playlists: the checks, the baselines, and the documents that said otherwise"
```

---

## 自审记录

**Spec 覆盖**：§3.1 三层描边 → Task 1/3/4；§3.2 令牌归属 → Task 1（不动 `MpvCardRingBrush`）；§3.3 浅色反转 → Task 1；§4.1 圆角 → Task 2/3；§4.2 按钮 → Task 2/3；§4.3 常态描边 → Task 4；§4.4 外框留槽（spike 后改写）+ 三处间距 → Task 0/3/5；§4.5 选中保留 → Task 6；§4.6 选择模式 → Task 7/8/9；§4.7 存储 → Task 10/11；§4.8a 添加到清单 → Task 9；§4.8b 行列表 → Task 12；§4.8c 改名删除 → Task 12；§4.8d 专辑卡不出勾选框 → Task 8。

**已知缺口**：§4.8d 的实现依赖「专辑卡用的是代表条目」，而 `LibraryEntry` 并不区分「这张卡代表专辑」。Task 8 Step 5 只做了单曲卡。计划里不假装它已完成——专辑卡的排除需要一个判据（按 `MusicGrouping` 与分区），留给执行时确认后再补一步。

**比例**：spec 约 400 行，本计划约 500 行，其中代码块是测试与签名，不是实现抄写。
