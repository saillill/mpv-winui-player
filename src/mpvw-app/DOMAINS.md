# 功能分区 · 索引（Domain map）

> 本文件是「分区上下文入口」的总表。每个分区的职责、对外接口、依赖和关键状态
> 写在该分区自己的 `README.md` 里，改一个区之前只读那一篇即可。
>
> 分区**不移动任何文件**——文件名与命名空间保持不变，分区由这份索引 + 各区 README
> 定义。理由见 `docs/domain-partition-plan-20261021.md` §6.1：物理移动会牵动 XAML
> 资源 URI、`mpv-conf-test.csproj` 的显式 `Compile Include`、以及
> `docs/settings-contract.md` §4 登记的已知例外，收益不抵风险。

## 分区表

| # | 分区 | 目录 | 文件数 | 上下文入口 |
|---|---|---|---:|---|
| 0 | 外壳 Shell | `Shell/` | 7 | [README](Shell/README.md) |
| 1 | 播放页 Player | `Modules/Player/` | 79 | [README](Modules/Player/README.md) |
| 2 | 设置 Settings | `Modules/Settings/` | 87 | [README](Modules/Settings/README.md) |
| 3 | mpv 配置 MpvConf | `Modules/MpvConf/` | 22 | [README](Modules/MpvConf/README.md) |
| 4 | 菜单 Menu | `Modules/Menu/` | 14 | [README](Modules/Menu/README.md) |
| 5 | 媒体信息 MediaInfo | `Modules/MediaInfo/` | 6 | [README](Modules/MediaInfo/README.md) |
| 6 | 本地化 Language | `Modules/Language/` | 9 | [README](Modules/Language/README.md) |
| 7 | 文件系统 FileSystem | `Modules/FileSystem/` | 5 | [README](Modules/FileSystem/README.md) |
| 8 | 激活 Activation | `Modules/Activation/` | 3 | [README](Modules/Activation/README.md) |
| 9 | 平台 Mpv (Win32) | `Modules/Mpv/` | 3 | [README](Modules/Mpv/README.md) |
| 10 | 应用模型 AppModel | `Modules/AppModel/` | 1 | [README](Modules/AppModel/README.md) |
| 11 | 关于 About | `Modules/About/` | 0（对话框在 `Player/MpvPlayerPage_MenuBar.cs`） | [README](Modules/About/README.md) |
| **I1** | **基础设施 · 动效** | `Modules/Motion/` | 5 | [README](Modules/Motion/README.md) |
| **I2** | **基础设施 · 公共** | `Modules/Common/` | 33 | [README](Modules/Common/README.md) |
| R | 资源 | `Assets/ Languages/ Menus/ MpvConfOptions/ Styles/ Themes/` | 69 | 见各区 README |
| C | 配置层（非 C#） | `data/` | 56 | [README](../data/docs/README.md) |
| N | 原生层（C++/WinRT） | `mpv-winui/mpv-winrt/` | 40 | 见 `AGENTS.md` |

文件数由 `Get-ChildItem -Recurse -File | Measure-Object` 实测（2026-10-22）。

## 依赖方向（硬规则）

```
Shell ──▶ Modules/*（视图）
                │
                ▼
         服务（Settings 持久化 / MpvConf / Language / FileSystem / Activation）
                │
                ▼
         传输层（mpv_winrt 投影 + MpvCommandQueue）
                │
                ▼
         基础设施（Motion / Common）—— 禁止反向依赖任何 Modules/*
```

允许的跨区引用只有这些：

| 从 | 到 | 为什么允许 |
|---|---|---|
| 任何区 | `Shell/AppContext` | 唯一全局服务出口 |
| `Player` | `Settings`(仅 `AppSettings` 数据模型) | 播放行为由设置驱动 |
| `Player` | `MpvConf`(只读 `MpvConfOverrides`) | 判断某选项是否被 mpv.conf 接管 |
| `Settings` | 所有区的服务 | 设置页是全应用控制面板（只读，不反向） |
| `Menu` | `MpvConf/Conf` | 菜单要写 `menu.conf` |
| 任何区 | `Language`、`Motion`、`Common` | 基础设施与词汇表 |

**禁止**：`Common`/`Motion` → 任何功能域；`Language` → 任何功能域；`MediaInfo` → `Player`。

## 术语

见 `docs/project-overview-20261021.md` §3。那份表是唯一口径，本目录不重复。

## 怎么用

1. 接到一个任务，先在本表定位分区。
2. 只读该分区的 `README.md`，再按它列的「入口文件」打开 1–3 个文件。
3. 需要跨区时，先看上面的白名单；白名单里没有就是设计问题，先讨论再加。
