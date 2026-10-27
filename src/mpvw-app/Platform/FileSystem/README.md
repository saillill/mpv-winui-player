# 分区 7 · 文件系统 / FileSystem

## 职责

路径、选择器、启动外部程序、以及**配置层部署**（把 `data/` 同步到
`%LOCALAPPDATA%\mpv-winui\mpv`）。

## 入口文件

| 文件 | 作用 |
|---|---|
| `ConfigDeployer.cs` | 首次运行 / 版本变化时部署配置层；按 manifest 区分"我们部署的 / 用户改过的" |
| `FileService.cs` | 文件枚举与媒体类型判定（链接进 `mpv-conf-test`） |
| `FilePickerHelper.cs` | 打开文件 / 文件夹 / 保存对话框 |
| `FileLauncher.cs` | 用默认程序打开、在资源管理器中显示 |
| `AppData.cs` | `%LOCALAPPDATA%\mpv-winui` 下的所有路径常量 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `ConfigDeployer` | `EnsureDeployed(mpvDir)` | `AppContext.Init()`（必须最先跑） |
| `AppData` | `MpvDir` / `LogsDir` / `CacheDir` … | 全应用 |
| `FilePickerHelper` | `OpenFileAsync()` 等 | 外壳、设置页 |

## 依赖

- 依赖：`Common/Utils`（`AtomicFile`）、`Language`。
- **不依赖** `Settings`、`Player`。

## 关键约定

- **`ConfigDeployer` 必须在任何配置写入之前完成**：`ManagedMpvConfig` 和
  `PluginConfigWriter` 是**合并**进已存在的文件，配置层不在磁盘上就无从合并。
- **部署是增量的**：只覆盖"我们部署且用户没改过"的文件；用户改过的保留。
  这条规则是 `mpv.conf` 曾被写坏之后加的，不要简化。
- **路径常量集中在 `AppData`**，不要在别处拼 `%LOCALAPPDATA%`。

## 上下文裁剪提示

改部署规则 → `ConfigDeployer.cs`（17.5 KB）+ `data/deploy-config.ps1`。
换路径 → 只改 `AppData.cs`，然后全库 grep 该常量确认没有重复定义。
