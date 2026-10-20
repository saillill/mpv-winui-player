# 本地化与菜单栏约定

## 两条文案渠道

1. WinUI 界面文案（按钮、菜单、对话框、设置页等）一律通过
   `AppLang` 属性读取，文案放在 `mpv-winui/mpv-winui/Languages/<lang>.json`
   （属性名 = JSON key，共 9 种语言，含 zh-TW）。
2. mpv 侧文案（右键菜单、OSD、脚本提示）沿用部分 Lua 脚本内置的 i18n 表
   （如 `dyn_menu.lua`、`select.lua`、`hdr_auto.lua`），这是独立渠道，
   不并入 AppLang；`zh-TW` 在 lua 读取点归一化为 `zh-CN`，避免 OSD 回退英文。
   （实测有 5 个脚本提到 `zh-TW`：`dynamic_menu.lua`、`dyn_menu.lua`、
   `hdr_auto.lua`、`save_global_props.lua`、`select.lua`。）

## `AppLang` 分三个文件（**改之前先确认改哪一个**）

| 文件 | 属性数 | 内容 | 可手改 |
|---|---:|---|---|
| `Modules/Language/AppLang.cs` | 1315 | 手写词汇 | ✅ |
| `Modules/Language/AppLang.Gen.cs` | 1096 | 548 条生成设置行的 `SchemaLabel_*` / `SchemaDesc_*` | ❌ 生成器覆盖 |
| `Modules/Language/AppLang.Sections.Gen.cs` | 26 | 13 个生成分区的标题与描述 | ❌ 同上 |

⚠️ **格式是 `public string X { get; set; }`，带空格。** 写正则统计属性时
漏掉空格会得到 0 —— 有些工具就踩过（`parse_applang_props` 一度写成
`\w+ \{ get; set; \}`，少一个空格就静默返回空集）。

⚠️ **顺带注意**：`CustomizeStrings.cs` 是一张**独立**的伴随表（49 个键），
**不是** AppLang 成员。它的类文档解释了原因：AppLang 属性靠反射按名读取，
无法同时承载译文。这 49 个键在 9 份 JSON 里都存在，而
`check-localization` 会把它们报成「没有 AppLang 属性的伴随表键（expected）」——
**这是预期的，不是缺陷**。

⚠️ **但「没有 AppLang 属性」不等于「可以留着」。** 这条 warning 曾经是
一个桶：任何没有属性对应的键都落进去，于是 11 个**设置早已删除**的键
（6 个 `SettingsTheme*` / `SettingsHelpTheme*`、`ThemeCustomName`、
4 个生成分区 `SectionGenGeneralPlatform|Terminal` 及其描述）在 9 份文件里
一直躺着，而检查器说「expected」。现在分成两类：

| 类别 | 判据 | 级别 |
|---|---|---|
| 伴随表 | 前缀 `Customize`，或键为 `ApplyAndExit`（`CustomizeStrings.Load` 读它们） | warning（预期） |
| 陈旧键 | 其余一切：无 AppLang 属性，也没有任何读取者 | **ERROR** |

判据是**有没有读取者**，不是键名像不像。删设置时顺手删掉它的文案键。

## 新增/修改文案的流程

1. 在 `AppLang.cs` 增加属性（默认值写英文），并保证属性名唯一。
2. 同步更新 9 份 `Languages/*.json`（缺 key 会导致校验脚本报错）。
3. 代码中只引用 `AppContext.AppLang.<Property>`，不在 XAML/C# 直接写面向
   用户的硬编码文本。

> 若改的是**生成行**的文案（`SchemaLabel_*` / `SchemaDesc_*`），
> 应改生成器或 schema，**不要手改 `AppLang.Gen.cs`** —— 下一次生成会覆盖。
> 那 1096 个键当前**在 9 份 JSON 里一个都没有**，所以生成行的标签与描述
> 一律显示英文；这是已知欠账（见设计约束 §7 A-16）。

> **编辑 JSON 的两条实测陷阱**（都已在本次工作里踩过）：
> 1. `Path.read_text()` **会规范化换行**。之后 `split("\r\n")` 得到的是
>    单条超长行，「这个键在不在」的答案一律为 False。用 `read_bytes()` 再
>    `decode`。
> 2. 不要用 `json.dump` 重写这些文件。它们是一个键一行、两空格缩进、CRLF、
>    无 BOM、以 `}\r\n` 结尾。重写会把 87 行的新增变成 1400 行的整文件重写，
>    而 `git diff --stat` 里看不出差别 —— 只能靠行数看出来。**按行增删。**

## 菜单栏

- 菜单结构（顺序、子菜单、图标、动作）由 `Menus/menus.json` 定义；
  文案通过 `labelKey` 引用 `AppLang` 属性，切语言时会重建菜单栏。
- 用户可以在 `%LOCALAPPDATA%\mpv-winui\mpv\menus.json` 覆盖内置配置
  （改顺序、增删项、加图标、加 mpv 命令项）；文件损坏时回退内置默认。
- 叶子项两种行为：`action`（必须是 `MpvPlayerPage_MenuBar.cs` 里
  `KnownMenuActions` 白名单的 id）或 `mpvCommand`（原始 mpv 命令）。
- 未知 `action`、未知 `labelKey`、既无 action 也无命令的项会被跳过并记录
  日志，不影响启动。
- **菜单栏的构建不依赖 mpv。** 它挂在 `MpvPlayerPage` 构造函数里，与
  `ApplyLocalizedStrings()` 并列（只依赖 `AppLang` + `menus.json`，两者在
  `AppContext.Init` 里已就绪）。曾经它只在 `MpvPlayerPage_Loaded` 的
  `if (_isPlayerInitialized)` 分支里构建 —— 也就是**必须先等 mpv 起来**，
  于是「中文出现慢」和「中文永远不出现」是同一个缺陷的两种表现。
  `_isPlayerInitialized` 分支里保留一次幂等重建。

## 校验

```powershell
python tools\check-localization.py              # 必查项
python tools\check-localization.py --xaml-audit # 额外输出硬编码文案清单
```

也可以 `.\build.ps1 -Configuration Debug -Platform x64 -CheckLocalization`
在构建后自动校验。注意 `-Debug x64` 这类缩写不可靠（`-Debug` 是 PowerShell
公共参数，不会设置 `-Configuration`），必须写全参数名。

**读输出时注意**：这个检查器把「缺键」按**家族**汇总（`1096x Schema*`），
而不是逐条打印 1096 个名字 —— 一行读不完的清单会被学会跳过。
