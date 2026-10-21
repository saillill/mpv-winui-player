# 分区 5 · 媒体信息 / MediaInfo

## 职责

用 `MediaInfo.dll` 解析当前（或指定）文件，把技术参数渲染成一份可读报告，
并给播放页提供"媒体信息"窗口。

**不负责**：音乐标签与封面（`Player/MusicTags.cs`、原生 `GetMediaTags`/`TryGetCoverArt`）。

## 入口文件

| 文件 | 作用 |
|---|---|
| `MediaInfoWindow.xaml(.cs)` | 独立窗口（由 `MainWindow.OpenMediaInfoWindow` 经 `WindowsManager` 打开，按路径去重） |
| `MediaInfoUserControl.xaml(.cs)` | 报告渲染控件（可复用于窗口与嵌入场景） |
| `MediaInfoReport.cs` | 报告分区与字段模型 |
| `NativeMediaInfo.cs` | `MediaInfo.dll` 的 P/Invoke 封装 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MediaInfoWindow` | 构造 `MediaInfoWindow(string? path)` | `Shell/MainWindow.OpenMediaInfoWindow` |
| `MediaInfoReport` | 解析结果 | `MediaInfoUserControl` |

## 依赖

- 依赖：`Common`（`BaseWindow`、`WindowsManager`）、`Language`。
- **不依赖 `Player`**。窗口只接收一个路径参数，不读播放器状态。

## 已知事实

- 窗口按 `"mediainfo_" + md5(path)` 去重，同一文件不会开两个窗口。
- `MediaInfo.dll` 来自外部，随构建拷入输出目录（`csproj` 的 `Content Include`，
  带 `Condition="Exists(...)"`，缺失时不影响构建但功能不可用）。

## 上下文裁剪提示

改报告字段 → 只读 `MediaInfoReport.cs`（7.8 KB）+ `MediaInfoUserControl.xaml`（8.5 KB）。
`NativeMediaInfo.cs` 只负责调 DLL，改字段不需要动它。
