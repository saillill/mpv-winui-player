# 构建输出 / 部署目录的布局（哪些能挪、哪些不能）

看构建输出或部署目录时，最扎眼的是**根目录堆着大量文件**（几百个 dll）。很容易想
"把这些 dll 收进一个文件夹"。**这个目录里它们不能挪** —— 下面是原因和代码依据，
免得以后再试一遍。

## 0. 两种输出，别搞混

这个仓库有两条不同的产物路径，布局**明显不同**，读的时候要先确认自己在看哪一个：

| | `dotnet build` 输出 | `dotnet publish` 输出 |
| :--- | :--- | :--- |
| 路径 | `mpv-winui\mpv-winui\bin\<Platform>\<Config>\net10.0-windows10.0.26100.0\win-x64\` | `mpv-winui\mpv-winui\bin\win-<Platform>\publish\` |
| 根目录文件 | 289 | 70 |
| 根目录 dll | 248 | 34 |
| 子目录 | 21（含 `Modules/ Shell/ Styles/ Themes/`） | 16 |
| 用途 | 开发/调试，`AGENTS.md` 工作流第 1 步 | 发布包（`package.ps1`、MSI、CI zip） |

**`Modules/ Shell/ Styles/ Themes/` 只存在于 build 输出。** `publish` 会把编译好的
`.xbf` 内联进 `resources.pri`，所以发布包里**看不到**这些目录，但应用照常加载页面 ——
不要因为在发布版里找不到 `Shell\MainWindow.xbf` 就以为打包漏了文件。

以下数字以 2026-09-18 的 publish 输出为准（上表为两种输出各自的实测值）：

```
publish 根目录：70 个文件 = 34 dll + 25 winmd + 6 pri + 2 exe + 2 pdb + 1 ico
16 个子目录 = 6 个应用自有 + 1 个框架 + 9 个框架语言
```

六个应用自有子目录：`Assets/ Config/ Languages/ Menus/ MpvConfOptions/
mpv-winui-lazy/`。

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

还有两个容易想挪走的非 dll 文件，同样**必须留在根**：

- **`App.ico`** —— 七处 `AppWindow.SetIcon("App.ico")` 传的是**相对路径**，
  相对的是进程工作目录（即 exe 所在目录）。移进 `Assets/` 会让所有窗口图标变白板。
- **`RestartAgent.exe`** —— Windows App SDK 的重启辅助进程，框架按固定位置查找。

判据很简单：**凡是加载器（WinRT 激活、XAML、.NET host）或代码按相对路径找的，
都不能动。**

## 1.5 两套语言资源，名字很像但无关（容易看错）

根目录有一批以语言代码命名的目录（`de-DE/ en-us/ es-ES/ …`，共 9 个），
它们**不是**本应用的语言文件，而是 Windows App SDK 框架自带的本地化资源：

```
de-DE/          Microsoft.ui.xaml.dll.mui
en-us/          Microsoft.UI.Xaml.Phone.dll.mui
zh-CN/          ...
```

每个目录里只有两个 `.mui`（框架 UI 的译文），没有别的。它们由框架自身加载，
和 `Languages/` 下的应用语言文件是**两套完全独立**的东西：

| | 位置 | 内容 | 谁读 | 能否增删 |
| :--- | :--- | :--- | :--- | :--- |
| 应用语言 | `Languages/<lang>.json` | 全部 AppLang 键值 | `LanguageManager`（路径硬编码） | 可增删，但改路径要同步改代码 |
| 框架语言 | `<lang>/`（根目录下） | 两个 `.mui` | Windows App SDK 自身 | 平台产物，不要动 |

一个语言要真正可用，需要 `Languages/` 下有对应 JSON。根目录有没有同名语言目录
**不影响**应用语言能否切换 —— 前者只决定框架自带控件（如文件选择器）的译文。
两处大小写风格不同（`en-us/` vs `en-US.json`），搜索时注意别只按一种写法找。

## 2. 已经分好组的子目录

下表以 publish 输出为准（16 个）。标了 "仅 build" 的目录只出现在 `dotnet build`
输出里，发布包中其内容已并入 `resources.pri`。

| 类别 | 目录 | 谁在用 |
| :--- | :--- | :--- |
| 应用自有 | `Assets/` | `FluentSystemIcons-Regular.ttf`（`AppContext.BaseDirectory`） |
| | `Config/` | `SettingsPage.Actions.Shortcuts.cs` 读 `BaseDirectory/Config/input.conf` |
| | `Languages/` | `LanguageManager` 读 `BaseDirectory/Languages/<lang>.json`（**路径硬编码**） |
| | `Menus/` | `menus.json` 菜单定义（`MenuDefinitionSource.BundledPath`） |
| | `MpvConfOptions/` | `mpv-options.json`，mpv.conf 编辑器的选项定义（`MpvConfSchemaService`） |
| | `mpv-winui-lazy/` | 部署到 `%LOCALAPPDATA%\mpv-winui\mpv` 的配置层 |
| | `Modules/` `Shell/` `Styles/` `Themes/`（仅 build） | 各页面编译出的 `.xbf`；publish 时内联进 `resources.pri` |
| 框架 | `Microsoft.UI.Xaml/` | WinUI 的 XAML 资源 |
| 语言 | `en-us/ de-DE/ …`（9 个） | Windows App SDK 的框架语言资源（`.mui`） |

`.xbf`（编译后的 XAML）必须**保持与源文件相同的相对路径**：`Shell/MainWindow.xaml`
编译出 `Shell/MainWindow.xbf`，应用按资源 URI `ms-appx:///Shell/MainWindow.xaml` 去找。
所以移动 `.xaml` 会同时改变输出里的 `.xbf` 路径，属于**预期行为**。

## 3. 给改代码的人的规则

1. **新增应用资源一律放子目录**，不要新增根级文件 —— 根目录已经只剩平台强制项。
   若新增子目录，记得同步 `mpv-winui.csproj` 的 `<Content>` 项并更新本文档。
2. **语言资源**只能用 `Languages/<lang>.json`，因为读取路径硬编码在
   `LanguageManager`；要改目录得同时改代码。
3. **不要移动 `.xaml` 的位置期望**：移动源文件即可（输出会自动跟随），
   但不要手工去搬输出目录里的 `.xbf`。
4. **部署方式**：`.\build.ps1 -Configuration Release -Platform x64` 产出 build 输出，
   `.\package.ps1 -Configuration Release -Platform x64 -SkipPublish` 产出发布 zip
   （`dist/mpv-winui-win-x64-Release.zip`）。根目录文件只做覆盖，不要增删，
   因为那些是平台文件。

   > 注：本文档早期版本提到一个 `probe/deploy_full.sh`，该脚本**不在本仓库中**，
   > 也不再是部署方式。请用上面的 `build.ps1` + `package.ps1`。
