# 构建输出 / 部署目录的布局（哪些能挪、哪些不能）

看构建输出或部署目录时，最扎眼的是**根目录堆着大量文件**（几百个 dll）。很容易想
"把这些 dll 收进一个文件夹"。**这个目录里它们不能挪** —— 下面是原因和代码依据，
免得以后再试一遍。

## 0. 两种输出，别搞混

这个仓库有两条不同的产物路径，布局**明显不同**，读的时候要先确认自己在看哪一个：

| | `dotnet build` 输出 | `dotnet publish` 输出 |
| :--- | :--- | :--- |
| 路径 | `mpv-winui\mpv-winui\bin\<Platform>\<Config>\net10.0-windows10.0.26100.0\win-x64\` | `mpv-winui\mpv-winui\bin\win-<Platform>\publish\` |
| 根目录文件 | 291 | 71 |
| 根目录 dll | 250 | 35 |
| 子目录 | 21（含 `Modules/ Shell/ Styles/ Themes/`） | 16 |
| 用途 | 开发/调试，`AGENTS.md` 工作流第 1 步 | 发布包（`package.ps1`、MSI、CI zip） |

**`Modules/ Shell/ Styles/ Themes/` 只存在于 build 输出。** `publish` 会把编译好的
`.xbf` 内联进 `resources.pri`，所以发布包里**看不到**这些目录，但应用照常加载页面 ——
不要因为在发布版里找不到 `Shell\MainWindow.xbf` 就以为打包漏了文件。

以下数字以 2026-10-09 的 publish 输出为准（上表为两种输出各自的实测值）：

```
publish 根目录：71 个文件 = 35 dll + 25 winmd + 6 pri + 2 exe + 2 pdb + 1 ico
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
- **.winmd 与 .pri**：25 个 `.winmd` **全部来自框架**（`Microsoft.Foundation.winmd`、
  `Microsoft.UI.Xaml.winmd`、`Microsoft.Windows.*.winmd` 等），供框架自身解析 WinRT
  类型；6 个 `.pri` 是资源索引，同样必须紧邻 exe。**这里最容易看错的是 `mpv_winrt`**：
  直觉会去找 `mpv_winrt.winmd`（以为它和 `mpv_winrt.dll` 成对用于 WinRT 激活），但发布
  包里**没有**这个文件 —— `unzip -Z1` 实测 `mpv_winrt.*` 只有 `mpv_winrt.dll` 与
  `mpv_winrt.pri`，25 个 winmd 里也没有它；而且运行期**不需要**它。原因是 `mpv_winrt`
  的 WinRT 投影（元数据）在**编译期**就被并进了应用本体：`mpv-winui.csproj` 的
  `CsWinRTIncludes=mpv_winrt` 让 CsWinRT 依据 `mpv_winrt.winmd` 生成投影代码、编进
  `mpvw.dll`，所以运行期要的是 **`mpv_winrt.dll` + `mpv_winrt.pri` 紧邻 exe**，而不是
  那个 winmd。把它当成"打包漏了文件"去找回来，是白费功夫。

所以：`mpvw.exe`（应用本体，`PublishAot=true` 编成原生可执行文件）、`mpv_winrt.dll`
（原生层）、`mpv-2.dll`（mpv 内核）、`MediaInfo.dll`、`WebView2Loader.dll` 以及全部框架
dll（含 `Microsoft.Web.WebView2.Core.dll`），**都留在根**。

> **最容易写错的两个名字是 `mpvw.dll` 与 `NLog.dll`**：它们**只出现在 `dotnet build`
> 输出**（`AGENTS.md` 工作流第 1 步的完成判据写的就是 `mpvw.dll`），**publish 里都没有** ——
> 应用因 `PublishAot=true` 被编成原生 `mpvw.exe`（没有托管 dll），`NLog` 被 trim／链接进
> `mpvw.exe`。这正是 §0「两种输出」要区分的东西，别把 build 侧的清单套到 publish 上。

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
| | `mpv-winui-lazy/` | 部署到 `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv` 的配置层 |
| | `Modules/` `Shell/` `Styles/` `Themes/`（仅 build） | 各页面编译出的 `.xbf`；publish 时内联进 `resources.pri` |
| 框架 | `Microsoft.UI.Xaml/` | WinUI 的 XAML 资源 |
| 语言 | `en-us/ de-DE/ …`（9 个） | Windows App SDK 的框架语言资源（`.mui`） |

`.xbf`（编译后的 XAML）必须**保持与源文件相同的相对路径**：`Shell/MainWindow.xaml`
编译出 `Shell/MainWindow.xbf`，应用按资源 URI `ms-appx:///Shell/MainWindow.xaml` 去找。
所以移动 `.xaml` 会同时改变输出里的 `.xbf` 路径，属于**预期行为**。

## 2.5 原生库（`libs/`）的按架构布局与平台矩阵

前面几节讲的是**输出**里哪些能挪。这一节讲**输入**：编译时用的原生库放在哪，
以及这个工程支持哪些架构。两者都会因为"看起来能合并"而被合错。

### 布局

```
mpv-winui\libs\
├── include\            头文件，与架构无关，所有架构共用
├── lib\<Platform>\     mpv.lib、mediainfo.lib、libplacebo.a
├── bin\<Platform>\     mpv-2.dll、MediaInfo.dll（运行时加载）
└── share\              mpv 数据文件
```

`<Platform>` 直接取 MSBuild 的 `$(Platform)` 原样拼写（`x64`、`ARM64`），
**不做名称映射** —— 这样目录名和构建平台不可能不一致。工程里由
`NativeLibraryLayout` 属性组（`mpv-winrt.vcxproj`）和 `Content` 条件
（`mpv-winui.csproj`）统一解析。

> 早期这里是**扁平**的 `libs\lib` + `libs\bin`，只能描述一个架构。这也是
> ARM64 接线缺失的根因：没有地方能表达"另一个架构的库在哪"。

`libs/` 不入库（见 `.gitignore`），由 `tools/fetch-mpv-libs.ps1` 安装：

```powershell
.\tools\fetch-mpv-libs.ps1 -Root . -Platform x64
```

脚本会**校验上游 release 页公布的 sha256**后才解压。这些二进制最终进入签名的
MSI 与发布 zip，未校验的下载是整条链上唯一一处被替换也无人察觉的环节。
版本与哈希写在脚本顶部，改版本必须同步改哈希，否则校验失败而不是静默接受新字节。

### 平台矩阵

| 平台 | 状态 | 说明 |
| :--- | :--- | :--- |
| `x64` | ✅ 完整可用 | 原生库来自上游，CI 与本地均可构建 |
| `ARM64` | ⚠️ 接线完整，缺原生库 | 见下 |
| `Win32` / `x86` | ❌ 已移除 | 上游无 x86 原生库，WinUI 3 桌面侧支持亦受限 |

**Win32 配置已从 `mpv-winrt.vcxproj` 删除。** 它此前被声明，但
`AdditionalIncludeDirectories`／`AdditionalLibraryDirectories`／
`AdditionalDependencies` 全部带 `|x64` 条件，所以那个配置从来不可能构建成功——
声明了一个不存在的支持。

**ARM64 的接线是完整的**（vcxproj 与 csproj 的条件、发布配置、输出目录都按
`$(Platform)` 解析），缺的只是**原生二进制**：本仓库使用的两个上游 builder
（`ikas-mc/mpv-windows-builder`、`ikas-mc/MediaInfoLib-builder`）**只发布 x64**。
要真正产出 ARM64 包，需要自备 aarch64 的 `mpv-2.dll` / `mpv.lib`（以及
MediaInfo），放到上面 `libs\bin\ARM64\` 与 `libs\lib\ARM64\` 即可，
其余流程无需改动。`fetch-mpv-libs.ps1 -Platform ARM64` 会直接说明这一点而不是假装下载。

### 缺库必须硬失败

两个工程各有一个守卫，目标架构缺原生库时**明确报错**：

| 工程 | 目标 | 报什么 |
| :--- | :--- | :--- |
| `mpv-winrt.vcxproj` | `CheckNativeLibraries`（`BeforeTargets="ClCompile"`） | 缺 `libs\lib\<Platform>\mpv.lib` 或 `libs\include\mpv\client.h` |
| `mpv-winui.csproj` | `CheckNativeRuntimeLibraries`（`BeforeTargets="BeforeBuild"`） | 缺 `libs\bin\<Platform>\mpv-2.dll` 或 `MediaInfo.dll` |

这是**替代一个静默失败**。此前 ARM64/Win32 构建只是拿不到 mpv 的
include/lib 路径，于是编译器报的是找不到某个头文件，而不是"这个架构没有库"；
应用工程更糟——`<Content>` 项只有 x64 条件，**没有一项匹配时什么都不会复制，
构建却成功**，于是产出一个没有播放器的包，直到用户按下播放才报缺 DLL。

判据：**目标架构的原生库缺失属于打包缺陷，必须在构建期以打包缺陷的形式报出来。**

### Windows App SDK 的版本规则（改这个之前先读）

应用工程用**元包** `Microsoft.WindowsAppSDK`（当前 2.5.1），不再逐个引用拆分包。
理由与取舍写在 `mpv-winui.csproj` 里那段注释中，要点：

- 拆分包的版本号互不相同是**产品设计**，不是缺陷；缺陷在于**跨家族混搭**。
  每个组件只依赖 Base，所以 NuGet 无法察觉错配——这正是之前的状态。
- 元包会额外带入 `Widgets`／`AI`／`ML`／`Search`。这些包**不是惰性的**：
  自包含部署会把它们的框架负载复制进发布输出，实测给一个视频播放器多带了
  约 56 MB 的 ONNX Runtime／DirectML／AI Fabric 程序集，所以这四个
  （连同 `Microsoft.Windows.AI.MachineLearning`）在 `mpv-winui.csproj` 中被
  `ExcludeAssets="all"` 排除。**排除是有意的**：需要其中某个功能时删掉对应那一行。
- `Microsoft.Windows.AI.MachineLearning` 的 buildTransitive targets 在
  `SupportedOSPlatformVersion < 18362` 时会**直接构建失败**，而本应用按 17763
  发布，所以那一层必须一起排除，只排四个是不够的。
- `mpv-winrt-projection` 刻意**只用单个 WinUI 组件**而不是元包：它自身要发布
  nupkg，引用图即消费者继承的引用图，没有理由让一个投影包拖进 AI/ML。
  它锁在 2.3.9 —— 正是 2.5.1 元包钉住的版本，所以不会漂出产品家族。
- **升元包时四处要一起动**：`mpv-winui.csproj` 的元包版本与四个排除项的版本、
  `mpv-winrt-projection.csproj` 的 WinUI 版本、以及 `mpv-winrt/packages.config`
  与 `mpv-winrt.vcxproj` 的 `PackageVersions` 属性组。排除项版本写死是刻意的：
  元包若抬高它们会触发 NU1605 降级错误而**不是**静默分叉。

C++ 侧（`packages.config`）**没有**直接列元包 id，而是按元包钉住的版本整组列出。
原因见 `mpv-winrt/packages.config` 顶部注释：元包的原生 targets 自带一个
`WindowsAppSDKVerifyTransitiveDependencies` 守卫，要求 `packages.config`
列出元包 nuspec 里的**每一个**依赖，对 2.5.1 就意味着还要列 Widgets/AI/ML/Search，
而原生播放器组件从不链接它们。彻底的修法是把该工程迁到 `PackageReference`，
让元包自己解析依赖，那是一个**需要真机编译验证**的构建系统改动。



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

5. **原生库按架构放**：新增或替换 `libs\lib\<Platform>\` 与 `libs\bin\<Platform>\`，
   **不要**把它们摊回 `libs\lib` / `libs\bin`（见 §2.5）。判据是 `$(Platform)`
   原样拼写，不要引入 `x64→amd64` 之类的映射表。
6. **不要声明没有原生库的架构**。`Win32` 就是因为"声明了但永远构建不出来"而被删除的；
   再加回一个架构前，先确认 `libs\lib\<Platform>\mpv.lib` 有来源。
   ARM64 的接线已经就绪，缺的只有二进制 —— 不要因为"现在构建不过"就把它的
   条件删掉，那正是它的守卫存在的意义。
7. **升 Windows App SDK 时四处一起改**（见 §2.5 末段），并跑
   `python tools/gen-schema-options.py --check` 确认生成物没有漂移。

   > 注：本文档早期版本提到一个 `probe/deploy_full.sh`，该脚本**不在本仓库中**，
   > 也不再是部署方式。请用上面的 `build.ps1` + `package.ps1`。
