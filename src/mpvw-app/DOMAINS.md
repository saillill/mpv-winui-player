# 功能分区 · 索引（Domain map）

本文件是分区的总表，只做索引。每个分区的职责、入口文件、对外接口和依赖写在
该分区自己的 `README.md` 里，改一个区之前只读那一篇。

分区是**逻辑**划分，不移动文件：目录即边界，命名空间即投影。物理移动会牵动
XAML 资源 URI、`mpv-conf-test.csproj` 的显式 `Compile Include` 和
[`settings-contract.md`](../../docs/settings-contract.md) §4 登记的例外，
收益不抵风险。

## 分区表

`文件` 与 `代码行` 只数 `.cs` / `.xaml`；行数按「数 `\n`，末尾无换行则 +1」统计。
重算命令见 [`architecture.md`](../../docs/architecture.md) §7。

| # | 分区 | 目录 | 文件 | 代码行 | 上下文入口 |
|---|---|---|---:|---:|---|
| 0 | 外壳 Shell | `Shell/` | 8 | 2542 | [README](Shell/README.md) |
| 1 | 播放页 Playback | `Domains/Playback/` | 78 | 28173 | [README](Domains/Playback/README.md) |
| 2 | 设置 Settings | `Domains/Settings/` | 64 | 26980 | [README](Domains/Settings/README.md) |
| 3 | 设置存储 SettingsModel | `Domains/SettingsModel/` | 16 | 5265 | 无 README，见下 |
| 4 | mpv 配置 MpvConf | `Domains/MpvConf/` | 29 | 7849 | [README](Domains/MpvConf/README.md) |
| 5 | 菜单 Menu | `Domains/Menu/` | 18 | 2007 | [README](Domains/Menu/README.md) |
| 6 | 媒体信息 MediaInfo | `Domains/MediaInfo/` | 6 | 745 | [README](Domains/MediaInfo/README.md) |
| 7 | 稍后观看 / 历史 / 命名播放列表 Playlist | `Domains/Playlist/` | 11 | 1532 | 无 README，见下 |
| 8 | 库类型 Library | `Domains/Library/` | 1 | 87 | 无 README，见下 |
| I | 基础设施 | `Infrastructure/` | 39 | 3524 | [Common](Infrastructure/Common/README.md)、[Motion](Infrastructure/Motion/README.md) |
| P | 平台 | `Platform/` | 14 | 2530 | [Activation](Platform/Activation/README.md)、[AppModel](Platform/AppModel/README.md)、[FileSystem](Platform/FileSystem/README.md)、[Win32](Platform/Win32/README.md) |
| L | 词汇表 Lexicon | `Lexicon/` | 9 | 3422 | [Language](Lexicon/Language/README.md) |
| R | 资源（非代码） | `Assets/`、`Languages/`、`Menus/`、`MpvConfOptions/`、`Styles/`、`Themes/` | — | — | 见 [`localization.md`](../../docs/localization.md) |
| A | 随包样例 | `AppData/` | — | — | [README](AppData/README.md) |
| C | 配置层（非 C#） | `data/` | — | — | [README](../../data/docs/README.md) |
| N | 原生层（C++/WinRT） | `src/mpvw-native/` | — | — | [`architecture.md`](../../docs/architecture.md) §1.2 |

`Infrastructure/` 与 `Platform/` 在表里按整层计；按子目录拆分后的读数见
[`architecture.md`](../../docs/architecture.md) §2.2。

**三个没有 README 的分区**，各自只有一两个文件，职责写在文件名里：

- `Domains/SettingsModel/`：`AppSettings*.cs` 的属性定义（`AppSettings.Gen.cs` 由
  `tools/gen-schema-options.py` 生成）、存储后端（`IDataSetting` 与三个实现）、
  值编解码（`SettingValueCodec`）。**它不引用任何 UI 域**，这是「设置存储可以
  独立测试」的结构基础。
- `Domains/Playlist/`：`WatchLater*` 与 `WatchHistory*` 两组解析器 + 控件，加上命名播放列表
  （`NamedPlaylist*`：一个 `.m3u` 的读写，以及它所在目录的存取）。三组互不依赖。
  队列本体不在这里，那是 mpv 的 `playlist` 属性，由 `Playback` 呈现。
- `Domains/Library/`：`VideoLibraryTypes.cs` 一个文件，库条目类型定义。库视图在
  `Playback`，所以方向是 `Playback → Library`，不是反过来。

## 依赖方向

方向与文件数见 [`architecture.md`](../../docs/architecture.md) §3 的实测边表，
那里有逐条证据。下面是规则，不是重复那张表。

硬规则：

| 规则 | 为什么 |
|---|---|
| `Infrastructure/` 不得引用任何域或 `Shell` | 它被所有域当作叶子依赖；加一条反向引用就形成环。唯一的既有例外是 `Infrastructure → Platform` 的 2 条（`LoggerHelper.cs`、`UpdateChecker.cs`），见 §3.1 |
| `Lexicon/` 不得引用任何域 | 实测出度为 0，它是纯词汇表 |
| `Platform/` 不得引用任何域 | 实测只有 3 条出边指向 `Infrastructure` |
| `SettingsModel` 不得引用任何 UI 域 | 设置存储要能脱离 UI 单测 |
| `MediaInfo` 不得引用 `Playback` | 报告模型独立于播放页；实测 `MediaInfo` 只依赖 `Infrastructure` |

这些规则都不只是约定：`architecture.md` §3 的脚本按目录归并 `using`，
所以违反会出现一条新的实测边，而不是一条没人看的注释。

## 怎么用

1. 接到任务，先在本表定位分区。
2. 读该分区的 `README.md`，按它列的「入口文件」打开 1–3 个文件。
   没有 README 的三个分区（SettingsModel / Playlist / Library）读上面那段说明。
3. 要跨区时先看 `architecture.md` §3 —— 那里是实测的，不是意图。
4. 数字要更新时跑 `architecture.md` §7 的命令，不要手改：本表与那份文档用同一套口径。
