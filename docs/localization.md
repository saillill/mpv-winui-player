# 本地化与菜单栏约定

## 两条文案渠道

1. WinUI 界面文案（按钮、菜单、对话框、设置页等）一律通过
   `AppLang` 属性读取，文案放在 `mpv-winui/mpv-winui/Languages/<lang>.json`
   （属性名 = JSON key，共 9 种语言，含 zh-TW）。
2. mpv 侧文案（右键菜单、OSD、脚本提示）沿用部分 Lua 脚本内置的 i18n 表
   （如 `dyn_menu.lua`、`select.lua`、`hdr_auto.lua`），这是独立渠道，
   不并入 AppLang；`zh-TW` 在 lua 读取点归一化为 `zh-CN`，避免 OSD 回退英文。

## 新增/修改文案的流程

1. 在 `AppLang.cs` 增加属性（默认值写英文），并保证属性名唯一。
2. 同步更新 9 份 `Languages/*.json`（缺 key 会导致校验脚本报错）。
3. 代码中只引用 `AppContext.AppLang.<Property>`，不在 XAML/C# 直接写面向
   用户的硬编码文本。

## 菜单栏

- 菜单结构（顺序、子菜单、图标、动作）由 `Menus/menus.json` 定义；
  文案通过 `labelKey` 引用 `AppLang` 属性，切语言时会重建菜单栏。
- 用户可以在 `%LOCALAPPDATA%\mpv-winui\mpv\menus.json` 覆盖内置配置
  （改顺序、增删项、加图标、加 mpv 命令项）；文件损坏时回退内置默认。
- 叶子项两种行为：`action`（必须是 `MpvPlayerPage_MenuBar.cs` 里
  `KnownMenuActions` 白名单的 id）或 `mpvCommand`（原始 mpv 命令）。
- 未知 `action`、未知 `labelKey`、既无 action 也无命令的项会被跳过并记录
  日志，不影响启动。

## 校验

```powershell
python tools\check-localization.py              # 必查项
python tools\check-localization.py --xaml-audit # 额外输出硬编码文案清单
```

也可以 `.\build.ps1 -Configuration Debug -Platform x64 -CheckLocalization`
在构建后自动校验。注意 `-Debug x64` 这类缩写不可靠（`-Debug` 是 PowerShell
公共参数，不会设置 `-Configuration`），必须写全参数名。
