# 06 · 主页图片卡片：封面不显示的根因线索

> **更正声明**：我在与用户的对话中曾说"模板里有 3 条 `{x:Bind}` 未写 `Mode`，导致 `OneTime` 不更新"。
> **这是错的。** 实测（剥离 XML 注释后统计）：全仓 **139 条 `x:Bind`，实际未写 `Mode` 的是 0 条**
> （唯一一处命中在注释里）。
> 真正的根因是下面这条，比"忘了写 Mode"严重得多。

---

## 一、根因（仓库内已有完整实测记录）

### 1.1 `WMC9999`：`{x:Bind}` 让 XAML 编译器内部崩溃

`MpvPlayerPage.xaml:340-341` 与 `MpvPlayerPage.Library.Cards.cs:768,799,917,956` 都记录了同一件事：

> 只要在专辑卡片模板里写一条 `{x:Bind AlbumTitle}`，WindowsAppSDK **2.3.9** 的 XAML
> 编译器就报 **WMC9999（内部错误，不指定文件、不指定行号）**。

因此专辑模板被迫改用 `{Binding}`。

**外部佐证**：[StackOverflow 同名问题](https://stackoverflow.com/questions/32850078) 记录了同样的
`Xaml Internal Error WMC9999: Object reference not set to an instance of an object`；
[microsoft-ui-xaml #11157](https://github.com/microsoft/microsoft-ui-xaml/issues/11157) 是另一个
WMC9999 变种（非英文 Windows 下的资源名不匹配）。

### 1.2 `{Binding}` 在 `ItemsRepeater` 的 DataTemplate 里不解析

`Library.Cards.cs:762-779` 记录：

- `ItemsRepeater` **不**给 realised element 设 `DataContext`（这是它与 `GridView`/`ListView` 的区别）
- 于是 `{Binding}` 全部解析为 null
- 实测现象：`stddev 0.0, distinct colours 1, whole card = rgb(32,32,32)` —— 整张卡片一个纯灰色矩形
- 对比：歌曲页签的模板全是 `{x:Bind}`，同一次实测 `stddev 17.1 / 94 colours`，正常

**于是代码走了第三条路**：`ElementPrepared` 里手动 `SetCardCaption` / `SetCardCover` /
`SetCardMediaSquare`（`Library.Cards.cs:805-808`），完全绕开绑定。

### 1.3 现行补丁的脆弱点

| 位置 | 现状 | 风险 |
|---|---|---|
| `SetCardCover`（`:830`） | 靠 `FindDescendant<Image>(card, "AlbumCardCoverImage") ?? ...("SongCardCoverImage")` 找元素 | **按名字字符串在可视化树里搜**，XAML 里改名不会报错，只会静默失败 |
| 封面填充 | 先读 `entry.Thumbnail`，再兜底查 `_libraryThumbnails` 缓存 | 已处理"回收卡片早于数据"的时序 |
| 延迟到达 | `SetCardCover` 里挂了**一次性** `PropertyChanged` 监听器 | 每个等待中的卡片一个监听器，靠首次触发时移除来防泄漏 |
| `SetCardMediaSquare` | 用代码算方形容器高度 | 因为 `{Binding ActualWidth, RelativeSource=Self}` 在该模板不解析，`{x:Bind}` 又会 WMC9999 |

---

## 二、线索清单

### 2.1 直接解决 WMC9999 的候选

| 线索 | 来源 | 内容 | 可复用 |
|---|---|---|---|
| **升级 Windows App SDK** | 本仓库 `docs/upgrade` 类记录；`mpv-winui.csproj` 钉 2.5.1，传递解析 `WindowsAppSDK.WinUI` **2.3.9** | WMC9999 **来自 2.3.9 这个子包**。nuget.org 有 2.6.4（元包与 WinUI 子包都有） | **最有希望**。但 `docs/upstream-gap.md` 与 `PIVOT-MIGRATION-NOTES.md` §五 已记录升级的连带影响：投影工程 `mpv-winrt-projection.csproj:31` 单独钉了 WinUI 2.3.9，C++ 的 `packages.config` 也钉了版本；且 `Microsoft.Windows.AI.MachineLearning` 没有 2.6.4 稳定版 |
| **`x:DataType` + `[Bindable]`** | 本仓库 `build-warning-budget.txt:26-29` 实测 | 对 `LibraryEntry` 加 `[Bindable]` → **CS8852**，生成器写不了 13 个 `init`-only 属性 | **已试过，失败** |
| **`x:SuppressXamlTrimWarnings`** | 同上 | 2.5.1 的编译器不认这个属性 | **已试过，失败** |
| **`{x:Bind}` 到 `x:DataType` 的 partial 类** | 通用做法 | 给卡片单独建一个 `x:DataType` 指向的轻量 VM，而不是直接绑 `LibraryEntry` | 未试过。**值得作为首选验证**：WMC9999 可能只针对某个特定类型投影 |

### 2.2 `ItemsRepeater` 的 DataContext 问题

| 线索 | 来源 | 可复用 |
|---|---|---|
| 官方 `ItemsRepeater` 文档 | [MS Learn](https://learn.microsoft.com/windows/apps/design/controls/items-repeater) | 确认"不设 DataContext"是否为设计如此，还是 2.3.x 的行为 |
| 改用 `ItemsView` | [ItemsView 指南](https://learn.microsoft.com/windows/apps/design/controls/itemsview) | `ItemsView` 由 `ItemsRepeater` + `ItemContainer` 组成，**是否修好 DataContext 需实测** |
| 改用 `GridView` | 平台控件 | 会设 DataContext，但失去 `UniformGridLayout` 的自定义能力 |
| `ElementPrepared` 手动赋值（现状） | 本仓库 | **已在用**。能用但脆弱 |

### 2.3 `x:Bind` 的已知缺陷（与图片无关，但同源，值得一并记录）

| 问题 | 来源 | 说明 |
|---|---|---|
| **`x:Bind` 导致 Window 引用环泄漏** | [microsoft-ui-xaml #7282](https://github.com/microsoft/microsoft-ui-xaml/issues/7282) | 用过 `x:Bind` 的 Window 关闭后不释放，反复开关内存持续增长；`Bindings.StopTracking()` 无效 |
| 社区修复包 | NuGet `hssong` 发布的 build-time patcher | 自动修改 XAML 编译器输出，在 `Window.Closed` 时打断循环引用 |

**对本项目的意义**：本仓库有 `SettingsWindow`、`MediaInfoWindow`、`PiPWindow` 等多个 Window，
反复开关是否泄漏未测。这是一条**独立的遗留风险**，与本项主题相邻但不相同。

---

## 三、适用性评估

| 线索 | 可用程度 | 接入成本 | 限制与风险 |
|---|---|---|---|
| **升级 WASDK 到 2.6.4** | **高** | **高**（3–5 人日） | 需同步改投影工程与 `packages.config`；`Windows.AI.MachineLearning` 无 2.6.4 稳定版；**升级后 WMC9999 是否消失未知**，必须先做最小验证再全面升级 |
| **独立 `x:DataType` VM** | 中 | 低（半天验证） | 若有效，可让模板回到纯 `{x:Bind}`，删掉全部手动赋值；若无效，成本也只是一次编译 |
| 换 `ItemsView` / `GridView` | 中 | 中 | 会牵动现有 `UniformGridLayout` 参数（`MinItemWidth=170` 等）与 `ElementPrepared` 回调 |
| 保留现状（手动赋值） | 高 | 0 | **能用，但 `FindDescendant` 按名字搜索是静默失败源**；XAML 改名不报错 |
| 社区 `x:Bind` 泄漏补丁 | 中 | 低 | 第三方包，需评估供应链；与本项主问题无关 |

### 已知的验证缺口

本仓库**没有**覆盖卡片渲染的自动化测试。`MpvPlayerPage.Library.Cards.cs` 的注释里
记录的都是"人工实测 + 截图统计颜色分布"（`stddev` / `distinct colours`）。
这意味着修复后**必须重复同样的像素统计**才算验证，不能只看编译通过。

---

## 四、检索提示词

### 4.1 搜索式

```
WMC9999 XamlCompiler internal error x:Bind DataTemplate
"WMC9999" WinUI 3 workaround x:DataType
ItemsRepeater DataContext not set DataTemplate binding null
WinUI 3 ItemsRepeater vs GridView DataContext difference
Windows App SDK 2.6 x:Bind compiler fix WMC9999
microsoft-ui-xaml issue 7282 x:Bind memory leak Window
WinUI 3 image not showing ItemsRepeater bitmap
```

### 4.2 交给后续会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。主页/媒体库的**专辑卡片封面不显示**
> 是个长期问题。
>
> **已查明的根因链**（不必重查）：
> 1. 在专辑卡片模板里写 `{x:Bind AlbumTitle}` 会让 WindowsAppSDK **2.3.9** 的 XAML 编译器
>    报 **WMC9999**（内部错误，无文件无行号）——`MpvPlayerPage.xaml:340-341`
> 2. 被迫改用 `{Binding}`，但 `ItemsRepeater` **不设 DataContext**，`{Binding}` 全部为 null
> 3. 于是 `ElementPrepared` 里手动 `SetCardCaption`/`SetCardCover` 绕开绑定
>    （`MpvPlayerPage.Library.Cards.cs:805-808`）
>
> **请先回答，不要改代码**：
> 1. 给卡片建一个独立的 `x:DataType` 轻量 VM（而非直接绑 `LibraryEntry`），能否绕过 WMC9999？
>    这是成本最低的验证，**先做这个**。
> 2. 若不能，升级 WASDK 2.3.9 → 2.6.4 是否值得？列出全部连带改动点
>    （`mpv-winrt-projection.csproj:31`、`src/mpvw-native/packages.config`、
>    `Windows.AI.MachineLearning` 无 2.6.4）。
> 3. 现状的 `FindDescendant<Image>(card, "AlbumCardCoverImage")` 按名字搜索，
>    XAML 改名会静默失败。是否有更安全的定位方式？
> 4. 修复后如何验证？仓库无自动化 UI 测试，历史做法是截图统计
>    `stddev` / `distinct colours`。请给出可复现的验证步骤。
>
> 参考：`docs/runtime-facts.md` §3（截图方法）、`tools/build-warning-budget.txt:26-29`（已失败的三种补救）。

### 4.3 深挖方向

- 先用最小改动验证"独立 `x:DataType` VM"能否绕过 WMC9999（半天内可出结论）
- 查 `ItemsRepeater` 官方文档确认 DataContext 行为是否版本相关
- 顺带测 `SettingsWindow` 反复开关是否有 x:Bind 泄漏（`#7282`）
