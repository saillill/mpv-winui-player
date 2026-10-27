# 分区 3 · mpv 配置 / MpvConf

## 职责

`mpv.conf` 的三件事：**读**（解析成结构）、**编辑**（带 schema 补全的编辑器）、
**写回**（应用的托管块 + 用户手改的保留）。

**不负责**：把设置值翻译成 mpv 命令（那是 `Domains/MpvConf/MpvSettings.cs`）；
运行期监听文件变化（`Domains/MpvConf/MpvConfWatcher.cs`，因为它要和 `MpvConfOverrides`
协同，放在 Settings 侧）。

## 入口文件

| 文件 | 作用 |
|---|---|
| `Conf/MpvConfManager.cs` | 解析 / 写回的核心（构造时接 `onBeforeSave` 回调，不反向依赖 Settings） |
| `Conf/MpvConfParser.cs` | 行级解析（`MpvConfLine` 是它的产物） |
| `Conf/MpvConfLine.cs` | 一行的模型（键、值、注释、是否被禁用） |
| `MpvConfEditorPage.xaml(.cs)` | 编辑器页面 |
| `MpvConfEditorWindow.xaml(.cs)` | 编辑器窗口壳 |
| `MpvConfTextWindow.xaml(.cs)` | 纯文本视图（25 KB，含查找/替换） |
| `MpvConfProfileItem.cs` | `profiles.conf` 的 profile 条目 |
| `Schema/` | `mpv-options.json` 的强类型模型 + 取值校验 |
| `Option/` | 选项控件（`MpvConfOptionControl` 等），与 Settings 的 `Option*Control` **不是同一套** |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MpvConfManager` | `Load()` / `Save()` / `Lines` | 编辑器、`ManagedMpvConfig` |
| `MpvConfSchemaService` | 按 mpv 选项名查 schema | 选项控件、设置搜索 |
| `MpvConfProfileItem` | profile 列表 | 编辑器 |

## 依赖

- 依赖：`Common/Utils`（`AtomicFile`）、`MpvConfOptions/mpv-options.json`（构建时拷入输出）。
- **不依赖** `Settings`。`MpvConfManager` 通过回调拿到"保存前要做什么"，
  这把 `mpv-conf-test` 从 WinUI 依赖里解耦出来了——**不要再把 Settings 引回来**，
  那会让整个测试工程编译不了（历史上发生过，两个已发布缺陷因此无人发现）。

## 上下文裁剪提示

改解析 → 只读 `MpvConfParser.cs` + `MpvConfLine.cs`，跑 `mpv-conf-test`（527 个用例）。
改 schema → `MpvConfOptions/mpv-options.json`（820 条）+ `Schema/`，改完跑
`tools/gen-schema-options.py --check`。
