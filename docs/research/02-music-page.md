# 02 · 音乐页：布局、视觉与交互线索

> **现状（实测）**：音乐库是播放页内的一个分区，三个下划线页签
> （Songs / Albums / Artists，`MpvPlayerPage.xaml:1156-1183`），已在用
> `ItemsRepeater` + `UniformGridLayout`（`MpvPlayerPage.Library.Music.cs:535`），
> `MinItemWidth=170 / MinItemHeight=246 / 列距12 / 行距20`，注释注明是照参考播放器量的。
> 专辑详情页存在（`OpenAlbum` / `ApplyAlbumChrome` / `ApplyAlbumArtworkAsync`）。
> **所以本文的落点不是"换控件"，是信息层次、视觉节奏与交互补充。**

---

## 一、线索清单

### 1.1 WinUI 3 原生、许可可复用

| 项目 | 来源 | 技术栈 | 核心功能 | 可参考 / 可复用 | 与本项目的关系 |
|---|---|---|---|---|---|
| **Fusion**（YesPlayMusic） | [zoinve/Fusion](https://github.com/zoinve/Fusion) | **WinUI 3 + WASDK + .NET 9**，**MIT** | 第三方网易云客户端：歌单、专辑、歌手、歌词、图片缓存 | **整份可读可抄**（MIT）。重点看 `src/YPM.UI/` 的列表/网格分层、图片缓存策略、歌词滚动 | 同为 WinUI 3 音乐界面，**许可最干净的一条**；架构上 `Core/Api/UI` 三层可对照本仓库的 `Domains/` |
| **Rhythm** | [FallenDeity/Rhythm](https://github.com/FallenDeity/Rhythm) | WinUI 3，.NET | 流媒体播放器：Main / Album / Artist / Playlist / Search 五个页面 | 页面划分方式；仓库里带 **`Settings.XamlStyler`** 配置文件（见第 07 项） | 页面级信息架构可对照；注意它依赖 Oracle + Supabase，**不能直接跑** |
| **mpv.net** | [mpvnet-player/mpv.net](https://github.com/mpvnet-player/mpv.net) | C# / WinForms + WPF | 桌面播放器，含媒体库视图 | 列表虚拟化与"正在播放"高亮的状态管理 | **GPL v2，只能读不能抄**（见许可提示） |

### 1.2 非 WinUI 但有成熟信息架构

| 项目 | 来源 | 技术栈 | 可参考部分 | 与本项目的关系 |
|---|---|---|---|---|
| **Tauon Music Box** | [Taiko2k/Tauon](https://github.com/Taiko2k/Tauon) | Python + GTK | **专辑/艺术家/播放队列的三种分组视图**，无缝播放，CUE 支持；UI 高度可定制 | 分组语义（Album Artist vs Artist）与"点专辑进详情"的导航模型可直接对照 |
| **MusicBee** | [getmusicbee.com](https://getmusicbee.com/) | Delphi/Win32，不开源 | 社区讨论里有大量**分组策略**的实践：[按 Album Artist 分组](https://getmusicbee.com/forum/index.php?topic=21130.0)、[Fluent Wall 专辑墙](https://getmusicbee.com/forum/index.php?topic=30818.0)、[Vinyl Crate 视图](https://getmusicbee.com/forum/index.php?topic=40409.0) | **只读设计思路**（闭源）。Fluent Wall / Vinyl Crate 是"把专辑当主角"的两种成熟做法 |
| **fooyin** | 见 [LinuxLinks 综述](https://www.linuxlinks.com/best-free-open-source-music-players/) | C++ / Qt | foobar2000 式可定制布局 | 布局可定制性的上限参考 |

### 1.3 平台控件与设计规范

| 文档 | 用途 |
|---|---|
| [ItemsView 指南](https://learn.microsoft.com/windows/apps/design/controls/itemsview) | WinUI 3 的新集合控件，由 `ItemsRepeater` + `ScrollView` + `ItemContainer` + `ItemCollectionTransitionProvider` 组成，**自带增删动画** |
| [集合与列表选型](https://learn.microsoft.com/windows/apps/develop/ui/controls/lists) | 官方给的"图像为主 → GridView；需自定义 → ItemsRepeater"判据 |
| [ItemsRepeater 高性能教程](https://cosmiclearn.com/winui/itemsrepeater.php) | `UniformGridLayout` 的实际写法 |
| Windows 11 照片应用 | 见 [TechRadar 报道](https://www.techradar.com/news/windows-11-finally-redesigns-its-photos-app-next-heres-what-it-looks-like)：圆角、渐变图标、浮动菜单。**这是同一平台的网格视觉基准** |

### 1.4 布局与分组的实证参考

| 来源 | 内容 |
|---|---|
| [IJMA 论文：grid vs list 布局对用户的影响](https://aircconline.com/ijma/V17N2/17225ijma01.pdf) | 学术侧对网格/列表两种专辑布局的对照实验，可用于论证"音乐页该用网格" |
| MusicBee 社区的分组讨论 | 真实用户在 `Artist` / `Album Artist` / `$Sort()` 之间的取舍，属于**产品决策的原始素材** |

---

## 二、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| **Fusion（MIT）** | **高** | 低（读代码） | 与网易云 API 强耦合，UI 层可借鉴、数据层不适用；.NET 9 与本仓库 net10 不冲突 |
| **Rhythm** | 中 | 低（读页面结构） | 依赖 Oracle/Supabase，跑不起来；只能读 XAML 结构 |
| **Tauon** | 中 | 低（读交互模型） | Python/GTK，**不能抄代码**；分组语义可借鉴 |
| **MusicBee 社区** | 中 | 低 | 闭源，只有设计讨论；但分组策略的取舍记录很有价值 |
| **mpv.net** | 低（有风险） | — | **GPL v2，禁止复制代码** |
| **platform 控件文档** | 高 | 低 | `ItemsView` 需要较新的 WASDK，本项目钉 2.5.1，**需先确认可用性** |

### 已识别的取舍点（供后续会话决策，不在本文定案）

1. **`ItemsRepeater` vs `ItemsView`**：后者自带 ItemCollectionTransition 动画，但要求新 SDK；本仓库已在用前者
2. **专辑墙（以封面为主）vs 列表（以信息密度为主）**：MusicBee 的 Fluent Wall 与 Vinyl Crate 是两种答案
3. **Artist 与 Album Artist 的分组语义**：直接影响"合辑"归到谁名下，是本项最容易做错的产品决定

---

## 三、检索提示词

### 3.1 搜索式

```
WinUI 3 music player album grid github
site:github.com WinUI3 "ItemsRepeater" album
MusicBee "Album Artist" grouping album compilation
Fluent Design album wall MusicBee
"ItemsView" WinUI 3 ItemCollectionTransitionProvider example
WinUI 3 image cache BitmapImage decode pixel width album art
music library UI "album artist" vs "artist" grouping UX
```

### 3.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`），音乐库是播放页内的一个分区，
> 三个页签 Songs / Albums / Artists，已用 `ItemsRepeater` + `UniformGridLayout`
> （`MinItemWidth=170`，`MpvPlayerPage.Library.Music.cs:535`）。
>
> 目标：改善音乐页的布局、视觉与交互。
>
> 请先回答，不要写代码：
> 1. `ItemsRepeater` 换成 `ItemsView` 的收益与代价？本项目钉 Windows App SDK 2.5.1，
>    `ItemsView` 是否可用？
> 2. 专辑墙与列表两种布局，各自适合什么使用频次？给出判据而不是偏好。
> 3. `Artist` 与 `Album Artist` 的分组差异会导致哪几类文件归错？本仓库
>    `MusicTags.cs` / `LibraryTagsReader.cs` 读了哪些字段？
> 4. 当前 `MinItemHeight=246` 是否留够了封面 + 两行文字 + 正在播放指示器？
>
> 参考可复用实现：`zoinve/Fusion`（WinUI 3，**MIT，可直接抄**）。

### 3.3 深挖方向

- Fusion 的 `src/YPM.UI/` 逐文件对照本仓库 `Domains/Playback/MpvPlayerPage*.cs`
- 查 `MusicTags.cs` 实际读取的标签字段，确认 AlbumArtist 是否已入库
- MusicBee 论坛搜 `album artist` 分类下的历史讨论，提取分组策略的失败案例
