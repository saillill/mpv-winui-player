# 构建输出 / 部署目录的布局（哪些能挪、哪些不能）

看构建输出或 `outputs/mpvw-test/` 时，最扎眼的是**根目录堆着约 290 个文件**
（248 个 dll）。很容易想"把这些 dll 收进一个文件夹"。**这个目录里它们不能挪** ——
下面是原因和代码依据，免得以后再试一遍。

以 2026-09-17 的实际输出为准：

```
根目录：291 个文件 = 248 dll + 25 winmd + 6 pri + 3 exe + 2 txt + 2 json
                   + 1 xml + 1 xbf + 1 pdb + 1 ico + 1 appxrecipe
19 个子目录 = 9 个应用自有 + 1 个框架 + 9 个语言
```

## 1. 为什么根目录的 dll 不能收进子目录

应用是**自包含**部署，两条设置决定了这一点（`mpv-winui.csproj`）：

```xml
<SelfContained>true</SelfContained>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
```

- **Windows App SDK 自包含**：`Microsoft.UI.Xaml.dll`、`CoreMessagingXP.dll`、
  `Microsoft.Internal.FrameworkUdk.dll`、`MRM.dll`、`DwmSceneI.dll` 等框架原生库由
  WinRT 激活与 XAML 加载器**相对 exe 位置**解析。挪走 = 应用起不来。
- **.NET 自包含**：`mpvw.deps.json` 记录的是**相对根目录**的程序集路径，
  `hostfxr.dll` / `hostpolicy.dll` / `coreclr.dll` 也必须在 exe 旁。
  改 deps.json 的路径不是受支持的用法，改完仍会连带影响框架解析。
- **.winmd 与 .pri**：25 个 `.winmd` 里有 `mpv_winrt.winmd`，它与 `mpv_winrt.dll`
  成对用于 WinRT 激活；6 个 `.pri` 是资源索引，同样必须紧邻 exe。

所以：`mpvw.dll`（应用本体）、`mpv_winrt.dll`（原生层）、`mpv-2.dll`（mpv 内核）、
`MediaInfo.dll`、`NLog.dll`、`WebView2Loader.dll` 以及全部框架 dll，**都留在根**。

判据很简单：**凡是加载器（WinRT 激活、XAML、.NET host）按固定规则找的，都不能动。**

## 2. 已经分好组的：19 个子目录

| 类别 | 目录 | 谁在用 |
| :--- | :--- | :--- |
| 应用自有 | `Assets/` | 图标、字体（`AppContext.BaseDirectory`） |
| | `Config/` | `SettingsPage.Actions.Shortcuts.cs` 读 `BaseDirectory/Config/input.conf` |
| | `Languages/` | `LanguageManager` 读 `BaseDirectory/Languages/<lang>.json`（**路径硬编码**） |
| | `Menus/` | `menus.json` 菜单定义 |
| | `Modules/` | 各页面编译出的 `.xbf`（WinUI 要求 .xbf 紧邻 dll 树） |
| | `Shell/` | `MainWindow.xbf` 等（2026-09-17 从根移入） |
| | `Styles/`、`Themes/` | 主题与样式资源 |
| | `mpv-winui-lazy/` | 部署到 `%LOCALAPPDATA%\mpv-winui\mpv` 的配置层 |
| 框架 | `Microsoft.UI.Xaml/` | WinUI 的 XAML 资源 |
| 语言 | `en-us/ de-DE/ …`（9 个） | Windows App SDK 的框架语言资源（`.mui`） |

`.xbf`（编译后的 XAML）必须**保持与源文件相同的相对路径**：`Shell/MainWindow.xaml`
编译出 `Shell/MainWindow.xbf`，应用按资源 URI `ms-appx:///Shell/MainWindow.xaml` 去找。
所以移动 `.xaml` 会同时改变输出里的 `.xbf` 路径，属于**预期行为**。

## 3. 给改代码的人的规则

1. **新增应用资源一律放子目录**，不要新增根级文件 —— 根目录已经只剩平台强制项。
2. **语言资源**只能用 `Languages/<lang>.json`，因为读取路径硬编码在
   `LanguageManager`；要改目录得同时改代码。
3. **不要移动 `.xaml` 的位置期望**：移动源文件即可（输出会自动跟随），
   但不要手工去搬输出目录里的 `.xbf`。
4. 部署靠 `probe/deploy_full.sh`。脚本会**清理**构建已不再产生的 `.xbf`
   与未使用的语言目录；根目录文件只做覆盖，因为那些是平台文件，本来就不该由我们增删。
