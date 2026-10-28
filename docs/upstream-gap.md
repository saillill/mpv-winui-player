# 上游差异清单

`mpvw` 与上游 `ikas-mc/mpv-winui-player` 的关系、许可边界，以及 15 条未纳入的提交逐条处置。

- 本仓库：`saillill/mpv-winui-player`（`mpvw`），LGPL-2.1
- 上游：`ikas-mc/mpv-winui-player`，对比到 `5b3d46a` 为止（见 §五的清单命令）
- 共同祖先：`2b74e2a`（2026-09-12）
- 上游领先 15 条（`2b74e2a..5b3d46a`）

## 一、许可边界

这条线决定了哪些提交可以移植、哪些只能自己重写。

| 提交 | 时间 | 动作 |
|---|---|---|
| `9a06de2` | 2026-10-18 12:27 | zip 解析修复 —— **LGPL-2.1 期** |
| `b5ddfb6` | 2026-10-18 15:26 | `Change LICENSE`：LGPL-2.1 → **GPLv3** |
| `5b3d46a` | 2026-10-25 12:12 | d3d11 显示信息修复 —— **GPLv3 期** |

本仓库 `LICENSE.txt` 与上游 `9a06de2` 的 `LICENSE.txt` **逐字节相同**（blob `8000a6faacf4`），
未跟随上游改成 GPLv3。

**因此**：`b5ddfb6` 之前的可以按 LGPL-2.1 移植；之后的不能直接取用代码，
只能读其行为描述后独立重写。这不是偏好，是许可约束。

## 二、逐条处置（15 条）

| 提交 | 日期 | 许可 | 文件 | 摘要 | 处置 |
|---|---|---|---:|---|---|
| `3ec602c` | 09-13 | LGPL | 1 | update README with package details | 不移植。README 内容上游化，本仓库 README 已重写 |
| `753cabf` | 09-14 | LGPL | 3 | menu editor 换行与新增子节点后选中 | **不移植**。本仓库菜单编辑器已独立演进（11 文件），改动点已不存在 |
| `276a7e0` | 09-15 | LGPL | 1 | fix track selector binding | **不移植**。本仓库音轨选择器实现不同（5 文件），需先复现 |
| `c426a73` | 09-18 | LGPL | 15 | add wizard for app conf data | **不移植**。本仓库无 `AppConfData` 功能线（0 文件命中） |
| `1a52bcb` | 09-18 | LGPL | 10 | 保持屏幕常亮 | **已具备**。本仓库走 mpv 的 `stop-screensaver`：默认值 `"yes"`（`AppSettings.Gen.cs:601`），映射在 `MpvSettings.Gen.cs:261,426`，设置行在 `SettingsPage.Options.Gen.cs:1589-1602`。不需要 `DisplayRequest` |
| `a12c6c4` | 09-19 | LGPL | 3 | 鼠标键显示/隐藏控制栏 | **已具备**。`HandlePlayControl` 在左键或中键按下时 `TogglePlayerControl()`（`MpvPlayerPage_PlayControl.cs:24-48`）；鼠标事件经 `MpvPlayerPage_Mouse.cs` 转发给 mpv |
| `8de677e` | 09-20 | LGPL | 8 | 单实例配置 | **不移植**。本仓库无单实例机制（0 文件命中） |
| `22f0e0c` | 09-22 | LGPL | 7 | history / watch later 对话框优化 | **不移植**。本仓库两者已整体重写（21 文件） |
| `7757cea` | 10-18 | LGPL | 3 | 窗口尺寸位置保存重构 | **不移植**。本仓库实现不同（18 文件命中） |
| `9a06de2` | 10-18 | LGPL | 3 | zip 解析修复 | **不移植**，理由见第三节 |
| `b5ddfb6` | 10-18 | — | 3 | Change LICENSE → GPLv3 | **不采纳**。本仓库保持 LGPL-2.1 |
| `d38d1ea` | 10-20 | GPLv3 | 1 | update deps | **不移植**（许可） |
| `0592827` | 10-20 | GPLv3 | 1 | MSIX BuildTools 包 | **不移植**（许可） |
| `be1b4c6` | 10-20 | GPLv3 | 1 | update package version | **不移植**（许可） |
| `5b3d46a` | 10-25 | GPLv3 | 5 | d3d11 composition 显示信息 | **本仓库已等效**，理由见第四节 |

统计：已具备 2 条、不移植 11 条（含 4 条许可限制）、不采纳 1 条、已等效 1 条。

## 三、`9a06de2`（zip 解析）：本仓库无此缺陷

上游该提交修的是 `AppConfDataImportService` 的 zip 归档解析：
条目路径过滤 `..`、目录树干构建、以及把 `ZipArchive` 的条目名规整成 Windows 路径。

**本仓库没有 `AppConfData` 功能线。** 核查：

```
git grep -l -i 'AppConfData' -- '*.cs' '*.xaml'   →  0 文件
```

本仓库的配置导入是另一条实现，在
`src/mpvw-app/Domains/Settings/SettingsPage.Actions.ImportExport.cs`：

- 只接受**单个 `.conf` 文本文件**（`FileOpenPicker` + `File.ReadAllLinesAsync`）
- 逐行 `key=value` 解析，空行与 `#` 注释跳过
- 没有任何 `ZipFile` / `ZipArchive` 调用

因此不存在 zip 路径穿越、目录树构建、条目名规整这三类问题。
**移植它等于把一段死代码搬进一个不调用它的项目。**

若将来要加 zip 归档导入，应作为新功能重新设计，而不是回填这段修复：
新功能的路径校验应在写入前用 `Path.GetFullPath` + 前缀比对，
而不是靠字符串里是否含 `..`——后者挡不住绝对路径 `/etc/…` 这一形态。

## 四、`5b3d46a`（d3d11 显示信息）：本仓库已等效

### 上游的做法

在 `MpvPlayer.cpp` 的 `UpdateDisplayColorInfo` 里按色彩类型直接 `SetOption`：

| 模式 | `d3d11-output-format` | `d3d11-output-csp` |
|---|---|---|
| HDR | `rgb10_a2` | `pq` |
| WCG | `rgb10_a2` | `srgb` |
| SDR | `rgba8` | `srgb` |

外加 `UpdateDisplayColorOptions(prim, trc, gamut…)` 写入 `target-prim` / `target-trc` / `target-gamut`。

### 本仓库的做法

同样的三条设置，但放在 `data/profiles.conf` 里，用 `profile-cond` 条件激活：

```ini
[mpvw-sdr]
profile-cond = p["user-data/mpvw/color-kind"] == "SDR"
d3d11-output-csp=srgb
d3d11-output-format=rgb10_a2

[mpvw-wcg]
profile-cond = p["user-data/mpvw/color-kind"] == "WCG"
d3d11-output-csp=bt.2020
d3d11-output-format=rgb10_a2

[mpvw-hdr]
profile-cond = p["user-data/mpvw/color-kind"] == "HDR" and (tonumber(p["user-data/mpvw/display-peak"]) or 0) > 0
d3d11-output-csp=pq
d3d11-output-format=rgb10_a2
target-trc=pq
target-prim=bt.2020
```

C++ 侧只发布触发条件：

```cpp
void MpvPlayer::UpdateDisplayColorInfo(DisplayColorKind colorKind)
{
    ...
    SetUserData("mpvw/color-kind", cs);   // "HDR" / "WCG" / "SDR"
}
```

`display-peak` 由 `MpvPlayerPage_Display.cs:423` 写入。

### 为什么这不算缺失

两者达成的结果相同：三种显示器色彩类型各自拿到正确的输出格式与色彩空间。
差别在**设置的归属**：上游把它编译进 C++，本仓库把它留在 mpv 的配置文件里。

后者有两个实际好处，都是上游那个写法做不到的：

1. **可改**。用户可以在自己的 `mpv.conf` 里覆盖任意一条，不必等新版本。
2. **可叠加 `profile-restore = copy`**。切换显示器时 mpv 自动回滚，不会残留上一个模式的值。

### 但有一条真实差异需要记录

上游的 `[mpvw-hdr]` 条件**没有** `display-peak > 0`，本仓库有。
若 `display-peak` 尚未写入，本仓库的 HDR profile 不会激活，而上游的会。

上游在此处其实有缺陷：`display-peak` 是字符串，`tonumber` 才是正确比较方式；
直接 `> 0` 在 `profile-cond` 里会运行时错误并导致 profile 永不生效。
本仓库的 `profiles.conf` 第 72 行注释记录了这一点。

**结论**：不需要移植。若要收窄差异，应改的是本仓库 HDR 条件里的 `display-peak > 0`
是否为必要门槛——这是产品判断，不是缺陷。

## 五、验证方式

```powershell
# 上游领先的提交清单
git log --oneline --reverse 2b74e2a..5b3d46a

# 许可分界：检查某个提交是否在 LICENSE 变更之前
git log -1 --format='%at' b5ddfb6          # 变更时刻
git log -1 --format='%at' <目标提交>        # 与之比较

# 本仓库许可证未跟随上游
git rev-parse 'HEAD:LICENSE.txt'            # 应为 8000a6fa…
git rev-parse '9a06de2:LICENSE.txt'         # 同上
git rev-parse '5b3d46a:LICENSE.txt'         # 应不同（GPLv3）

# 本仓库无 AppConfData 功能线
git grep -l -i 'AppConfData' -- '*.cs' '*.xaml'

# 色彩设置的两处来源
git grep -n 'd3d11-output' -- 'data/profiles.conf'
git grep -n 'SetUserData("mpvw/color-kind"' -- 'src/mpvw-native/MpvPlayer.cpp'
```

## 七、结构与实现上的主动差异

以下几处本仓库与上游同名文件的做法不同。**都不是缺陷**，是有原因的选择；
原因成立就不该「改回上游写法」。

### 7.1 `Keycodes.cs`：删了键名表，只损失 trace 级日志

上游 `Keycodes.cs` 610 行，含 `key_names` / `modifier_names` 两张表与
`mp_input_get_key_name` / `mp_input_get_key_from_name`；本仓库 270 行，这些全部删掉。

**不是功能缺失**，依据：

- 这一层唯一还在 trace 分支里的地方就是输入转发：
  `MpvPlayerPage_InputForwarding.cs:215`、`MpvPlayerPage_Mouse.cs:227,261` 各有一处
  `if (_logger.IsTraceEnabled)`，那里本来是唯一会调键名函数的位置；
- 本仓库的 `SendKeydown` / `SendKeyup`（`MpvPlayerPage_InputForwarding.cs:62,75`）
  与上游逐行等价，只多出 trace 代码块留下的空花括号；
- 这两个函数名在 `*.cs` / `*.cpp` / `*.h` 里 0 命中（只出现在本文）。

代价：排查按键问题时少了一条「按键名 → 键码」的 trace 输出。

### 7.2 `MpvPlayerPage_InputForwarding.cs`：逻辑等价 + 增强

上游对应的文件是 208 行（名字不同，无法按本仓库路径校验）；本仓库承担同一职责的
`MpvPlayerPage_InputForwarding.cs` 是 274 行，
方法齐备并多出 `IsSliderNavigationKey`、`IsTextInputFocused`、`SendMouseButton` 等。
**不算偏离**——上游有的这里都有。

### 7.3 `WindowStyleManager.UpdateBackdrop()`：改成无参、内部读设置

上游签名 `UpdateBackdrop(string backdropType)`，由调用方指定材质；本仓库改成无参
`UpdateBackdrop()`，内部读设置，另加 `UpdateUiFont`、`ResolveTheme`。
调用方因此不需要知道材质从哪来。

### 7.4 目录是拍平的

上游 `Modules/Player/PlayerControl/*`；本仓库拍平为 `Domains/Playback/*`
（文件内容基本沿用上游）。XAML 移动是自传播的：`.xaml` 编译成同相对路径的 `.xbf`，
页面按资源 URI 加载，所以移动源文件后输出跟着走。

### 7.5 `PlayerTrackSelectorControl` 与 `MenuConfWriter` 已对齐上游

- `PlayerTrackSelectorControl.xaml:40,81,122,163` 四处 `SelectedIndex` 绑定都带 `Mode=OneWay`。
- `MenuConfWriter.cs:27,45` 用 `Append('\n')` 而非 `AppendLine()`（注释在 `:23`、`:44`，写明平台换行
  会破坏 `menu.conf`）；测试直接比原始字节，CRLF 会失败。
