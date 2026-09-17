# upstream-merge 与上游的差异清单

> 基线（2026-09-17 实测）：
> - 本地 `upstream-merge` = `3048aeb`
> - 上游 `origin/main`（ikas-mc/mpv-winui-player）= `276a7e0`
> - 共同基点（merge-base）= `2b74e2a`
>
> 本文全部结论来自逐文件对比与全库 grep，未沿用任何旧结论。

## 1. 规模

| | upstream-merge | 上游 origin/main |
| :--- | :--- | :--- |
| 跟踪文件数 | 503 | 300 |
| 领先提交 | 382 | 3 |
| 本地化 | 9 个 JSON + `AppLang.cs` 1789 行 | 无 JSON，`AppLang.cs` 54 行 |
| 自检脚本（`tools/`） | 6 个 | 无 |
| 原生 API 声明（`MpvPlayer.idl`） | 101 | 87 |
| `data/` | 8 个文件 | 8 个文件（内容一致） |

## 2. 我们比上游强的地方

1. **本地化**。上游 `AppLang.cs` 只有 54 行、没有任何语言文件；我们是 1789 行 +
   `Languages/*.json` 九种语言、1225 条文案，并配 `tools/check-localization.py` 做一致性检查。
2. **设置界面**。上游是平铺的选项列表；我们改成卡片 + 二级文件夹，支持拖动排序、
   新建文件夹、隐藏/显示、重命名（`SettingsPage.xaml.cs` 我们比上游多 1099 行）。
3. **自研控制栏**。上游把控制栏塞在单个 `PlayerControl.cs` 里；我们拆成
   `ControlBarLayoutEngine` / `ControlBarLayoutGrammar` / `ControlBarAdaptiveLayout` /
   `PlayerControl.Preview` / `PlayerControl.ProgressMarks` / `PlayerControl.PanelAnimation` 等。
4. **快速控制面板**。`QuickControlPanel.{Audio,Video,Subtitles,Builders}`，上游没有对应实现。
5. **菜单编辑器**。`MenuEditorPage_{TreeView,Drag,Preview,Conf}` + `MenuConfWriter`，
   上游只有更薄的 `data/menu.json` 方案。
6. **原生层是严格超集**。101 vs 87 条声明，且上游**没有任何我们没有的 API**。
   我们多出 `AbLoopA/B`、`ApplyCommandStrings`、`GetCurrentFilePath`、`GetGpuAdapters`、
   `ObserveProperty`/`UnobserveProperty`、`SetLogLevel`、`VoConfigured` 及一批事件。
7. **配置层的工程化**。`ConfigDeployer` 按 manifest 区分"我们部署的 / 用户改过的"，
   增量更新并清理不再提供的文件；上游的 `data/mpv/*` 只是静态拷贝。
8. **自检工具**。`tools/` 下 6 个脚本（本地化、设置漂移、界面文案、分类表生成等），上游没有。

## 3. 上游比我们强的地方

### 3.1 架构：上游删掉了包装层，我们还留着兼容垫片

`MpvPlayerCompat.cs`（200 行、19 个扩展成员）注释里自述了这件事：

> The upstream refactor removed the `MpvMediaPlayer` wrapper class and made the WinUI layer
> talk to the native WinRT object directly. The fork's own partial files (PlayerControl.\*,
> MpvPlayerPage\_\*, PiPWindow) still call the shape the wrapper used to expose:
> `player.Native.X` for events, and simple properties such as `Volume` / `Position` / `Playing`
> instead of the native `Volume()` / `Position()` / `IsPaused()` methods.

即：**上游把全部调用点迁移到了原生 API，我们没有迁移，而是加了一层垫片把旧形状映射回去。**
`MpvPlayerPage.ForkCompat.cs` 是同一问题的另一面 —— 合并时我们的 partial 文件被上游版本替换，
fork 专有的辅助函数只能集中挪到新文件里放回。

这是目前**维护成本的主要来源**，也是下次合并上游时最容易冲突的地方。

### 3.2 三个上游提交没有合过来

| 上游提交 | 内容 | 我们缺什么 | 影响 |
| :--- | :--- | :--- | :--- |
| `276a7e0` | fix track selector binding | 4 处 `{x:Bind XxxListView.SelectedIndex}` 加 `Mode=OneWay`；我们的 `PlayerTrackSelectorControl.xaml:41,82,123,164` 仍是默认的 OneTime | **真 bug**：轨道面板标题上的序号不随选择刷新 |
| `753cabf` | optimize menu editor | ① `MenuConfWriter` 用 `Append('\n')` 取代 `AppendLine()`，我们 `MenuConfWriter.cs:23,40` 仍是 `AppendLine()`；② 新增 `SelectNodeInTree` / `ExpandNodeInTree`，我们两个都没有 | ① 生成的 `menus.conf` 是 CRLF，与上游不一致；② 菜单编辑器**新增节点后不选中、不展开**，新建的子节点看不见 |
| `3ec602c` | update README with package details | — | 本轮 README 已重写，可忽略 |

⚠️ ① 之所以一直没被发现，是**被我们自己的测试掩盖了**：`MenuConfWriterTests.cs:33` 仍保留
`NormalizeNewLines` 辅助函数，把 CRLF 归一化之后才断言，所以测试一直是绿的。
上游的做法是删掉该辅助函数，让测试直接暴露真实字节。

### 3.3 我们丢掉了上游的按键 trace 日志

上游 `Keycodes.cs` 610 行，含 `key_names` / `modifier_names` 两张表与
`mp_input_get_key_name` / `mp_input_get_key_from_name`；我们只有 270 行，这些全部删除。

**核查结论：不是功能缺失，只损失 trace 级日志。** 依据：

- 上游对这两个函数的**全部**调用点都在 `if (_logger.IsTraceEnabled)` 内
  （`MpvPlayerPage_Input.cs:60,151`、`MpvPlayerPage_Mouse.cs:263`）；
- 我们的 `SendKeydown` / `SendKeyup`（`MpvPlayerPage_InputForwarding.cs:62,75`）与上游逐行等价
  （逐行 diff 只多出 trace 代码块留下的空花括号）；
- 全库 grep 无其他消费者。

所以这是一次得当的清理。代价是：排查按键问题时，少了一条"按键名 → 键码"的 trace 输出。

## 4. 「合并了上游代码，但没按上游逻辑走」的清单

| 位置 | 上游逻辑 | 我们的做法 | 判定 |
| :--- | :--- | :--- | :--- |
| `MpvPlayerCompat.cs` | 删除包装层，调用点直连原生 API | 保留包装层形状，加 19 个扩展方法映射回原生（文件自述可整体删除） | **偏离** |
| `MpvPlayerPage.ForkCompat.cs` | 辅助函数留在各自的 partial | 因合并替换了我们的 partial，fork 专有函数被集中挪到新文件 | **偏离（合并产物）** |
| `MpvPlayerPage_InputForwarding.cs` | 上游文件名为 `MpvPlayerPage_Input.cs`（208 行） | 我们 294 行，方法齐备并多出 `IsSliderNavigationKey`、`IsTextInputFocused`、`SendMouseButton` 等 | 逻辑等价 + 增强，**不算偏离** |
| `PlayerTrackSelectorControl.xaml` | 已修 `Mode=OneWay` | 沿用上游文件，但没跟这个修复 | **偏离（见 3.2）** |
| `MenuConfWriter.cs` | 已改为 LF 输出 | 沿用上游文件，但没跟这个改动 | **偏离（见 3.2）** |
| `WindowStyleManager.cs` | `UpdateBackdrop(string backdropType)`，由调用方指定材质 | 改成无参 `UpdateBackdrop()`，内部读设置；另加 `UpdateUiFont`、`ResolveTheme` | 有意为之 |
| 目录结构 | `Modules/Player/PlayerControl/*` | 拍平为 `Modules/Player/*`（文件内容基本沿用上游） | 有意为之 |

另外，`Modules/Mpv/Keycodes.cs` 的裁剪（见 3.3）虽然也是"改动上游文件"，
但改的是上游可以删掉的东西，不构成逻辑偏离。

## 5. 建议

按性价比排序：

1. **合 `276a7e0`**：4 行改动，修掉一个真实 bug（轨道序号不刷新）。
2. **合 `753cabf`**：`SelectNodeInTree` / `ExpandNodeInTree` 直接改善菜单编辑器手感；
   LF 那处顺带删掉测试里的 `NormalizeNewLines`，让测试不再掩盖差异。
3. **想让下一次合并上游更省事，再动 `MpvPlayerCompat.cs`**：
   逐步把调用点迁到原生 API，迁完删掉垫片，同时把 `ForkCompat` 里的函数归回各自的 partial。
4. 其余差异（设置界面、本地化、控制栏、配置层）是本分支的主动选择，保持即可。
