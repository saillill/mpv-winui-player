# 运行时事实与已验证的机制

本文收集**只有亲自跑过才知道**的事实：进程行为、路径、测量结果、以及"看起来该这样、其实不是"的坑。
每一条都注明了验证方式和日期，因为这类事实会随版本漂移，而漂移之后若还留在文档里就会主动误导。

判据：不写设计意图（那在 [`architecture.md`](architecture.md)），不写规范（那在
[`settings-contract.md`](settings-contract.md)），只写"代码里读不出来、跑一次才知道"的东西。

---

## 1. 路径：日志、配置、设置

三条路径都有一个**同名目录套两层**的形状（`mpv-winui\mpv-winui\`），少写一层会得到一个
存在但从不更新的目录。

| 什么 | 真实位置 |
|---|---|
| 应用日志 | `%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\mpv-winui.<date>.log.txt`，另有 `display-info.log` |
| mpv 配置目录 | `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\` |
| 设置 | 注册表 `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app`，布尔值存成字符串 `"true"` / `"false"` |

`%LOCALAPPDATA%\mpv-winui\logs\`（只有一层）**也存在**，是个诱饵：它的最新文件停在
2026-09-25，而活的那份每次运行都写。按单层路径去找会得出"日志是空的"这个错误结论。

`%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf` 同样是诱饵（少一层目录）。

**但覆盖方向与本节此前的说法相反，2026-10-29 实测更正。** 旧文说
「`Initialize` 在 `mpv_initialize` 之前设的选项会覆盖配置文件的同名项」——**恰恰说反了**。
mpv 在 `mpv_initialize` 期间读配置文件，**配置文件的值会覆盖**此前用
`mpv_set_option_string` 存进去的值：

| 配置 | 初始化前设 `force-window=no` | 读回 `force-window` |
|---|---|---|
| `config=yes`（默认） | 是 | **`true`**（被 `data/mpv.conf:35` 覆盖） |
| `config=no` | 是 | **`false`** |

第二行是对照组：选项本身两种情况下都被接受，是配置文件赢了。所以想在正确的那份 `mpv.conf`
里生效，改那一份就行；想让**程序**说了算，必须在 `mpv_initialize` **之后**用
`mpv_set_property_string` 再设一次（init 之后 `mpv_set_option_string` 是 no-op）。

这条错误判断的实际代价：视频一直渲染在 mpv 自己的顶层窗口里，`SwapChainPanel` 全空，
而“程序设置会赢”这个错误前提让人不去查配置层，绕了很久。现在 `Initialize` 在两处都设，
后一处用属性 API，并把两者写进注释。

`ConfigDeployer` 每次启动都把随包的配置层重新展开一次，只放过 `UserOwnedFiles`
（`mpv.conf`、`input.conf`、`menus.json`、`custom_menu.json`）和 `UserOwnedDirs`（`script-opts`）。
`mpv/scripts/` **会**被重新部署，所以想排除某个脚本要改目录名，`load-scripts=no` 不管这件事。

---

## 2. mpv IPC 管道

管道名 `\\.\pipe\mpvpipe`。三条实测结论（2026-10-26 复测，纠正了此前"只在启动后约 1 秒内可连"
的说法，那条在两个方向上都错）：

1. **任何时刻都能连上。** 在一个全新进程上，+1.05 s 和 +6.05 s 各连一次都成功。
2. **连上的那一条大约 0.4 秒后会自己断。** 即使每 100 ms 做一次 `get_property` 往返，
   也在 +1.42 s 开始写失败（`err=232`，`ERROR_NO_DATA`）。所以"保持忙碌"不能续命。
3. **同时只允许一个客户端。** 第二个连接会在 `write 232` / `read 109`
   （`ERROR_BROKEN_PIPE`）之间来回。

实用规则：**连上就立刻写**，把要读的全在一次突发里读完，下一次突发重新连接。
不要在 connect 和第一次 write 之间 `sleep`。

协议是每行一个 JSON：`{"command":[...],"request_id":N}`，读到 `request_id` 匹配为止。
`time-pos` 在没有加载媒体时返回 `property unavailable`——那是正常答复，不是失败。

---

## 3. 截图与 UI Automation

**`PrintWindow` 截不到 WinUI 的浮出层，这不是渲染缺陷。** WinUI 的浮出层是**独立的顶层
HWND**（`className: "Microsoft.UI.Content.PopupWindowSiteBridge"`，`ownerHwnd` 指向主窗口，
`label: "popup"`），对主窗口句柄调用 `PrintWindow` 没有东西可画。2026-10-27 用
`winapp ui list-windows -a mpvw` 在打开排序浮出层后测得：两个窗口，浮出层 240x272，
owner 是主窗口。

截图手段按优先级：

1. `winapp ui screenshot -a <app>` —— 会把应用自己的窗口**连同它拥有的浮出层**一起合成，
   JSON 里列出两个窗口并报 `captured: true`。唯一不要求窗口在前台的办法。
2. `CopyFromScreen` —— 能用，但要求窗口可见、在前、没被遮挡，而且会把挡在前面的东西
   一起拍进去（曾有一次把一款无关的全屏游戏拍进"应用坏了"的截图）。

**UIA 的 `BoundingRectangle` 是物理像素，没有缩放系数。** 这条曾被写成"本机有 ×1.5 缩放"，
两个地方都错：本机是 **1.75**（168 DPI / 175%），而且**任何**系数都不该乘。2026-10-27 在
hwnd 197366 上实测：`GetWindowRect` = 264,264,3144,1834，`UIA BoundingRectangle` 也是
264,264,3144,1834，完全一致（测量进程已设 Per-Monitor-V2）。`winapp ui inspect` 自己的
表头也写着 `windowDpi:168, scale:1.75, coordinateSpace:"physical-screen-pixels"`。

那个 ×1.75 神话有个可复现的来源：**DPI 不感知**的调用方（默认的 pwsh 就是）对同一窗口
调用 `GetWindowRect` 得到被虚拟化的 980x840，而 UIA 报 1715x1470，比值正好 1.75，看起来
就像"UIA 加了系数"。其实没有——是被调用方被虚拟化了。`SetProcessDpiAwarenessContext(-4)`
之后两者一致。需要 DIP 时：`DIP = 物理像素 / 1.75`。

**整窗截图可以是"不存在的缺陷"的证据。** 窗口投影会在左边缘读成一条竖排文字，播放内容透出
面板会读成渲染错误。先看像素再信图片——一个会话里有五个"缺陷"是这么被否掉的。

**UIA 读得进这个 WinUI 3 窗口**（`WinUIDesktopWin32WindowClass`，约 60 个 AutomationId：
`ShellNav`、`ShellTitleText`、`ProgressSlider`、`PlayQueue`）。`Grid` 不是 UIA 节点，
所以看得见的容器可能没有对应元素——断言它的子元素。导航项需要显式
`AutomationProperties.SetAutomationId` 才可寻址（`NavigationViewItem.AutomationId` 不编译）。

---

## 4. 打开应用、关闭应用

- **启动是允许的**：可以看、可以走 IPC、可以跑 UI 自动化。行为验证是预期之内的事。
- **绝不能留着它跑**：每个脚本都要自己关掉并断言进程已消失。这条从未放宽过。
- **用开关关，不用 `Stop-Process`**：`pwsh tools/deploy-test.ps1 -CloseRunning -Verify`。
  `-CloseRunning` 先发 `WM_CLOSE`，只对 10 秒仍不理会的窗口强杀，这样 WinUI 能正常退出、
  mpv 能刷日志和存状态。不要往正在运行的安装里部署。

---

## 5. 定位崩溃

设置窗口崩溃报 `0xc000027b` + `Microsoft.UI.Xaml.dll`，没有对话框、没有托管栈、没有日志行，
那是**stowed exception**：异常从点击处理器逃进了 WinUI 自己的派发，被它吞掉。

**先看 WER 报告的 `P7` 字段。** `0x8013xxxx` 指向托管代码：`0x80131509` 是
`COR_E_INVALIDOPERATION`（即 `InvalidOperationException`），`grep "throw new"` 几秒就能找到。
不要从读 XAML 开始。

这一类最便宜的信号是**兄弟方法之间的保护不对称**：`OptionLayoutControl` 的 `ToggleEdit`
把工作包在 try/catch 里，`Select` 没有；而 `Select` 对 `panel.Children[0]` 用的是邻居给
索引 1 写的那个 `Count > 1` 守卫——它盖不住 0。**对比兄弟方法比通读文件有效。**

---

## 6. 控制栏、浮出层与播放模式

- **位置 UI**：事件驱动。`time-pos` 由原生侧观察 + 100 ms 合并定时器——不要重新引入轮询。
- **全屏**：由 mpv 的 `fullscreen` 属性驱动（`HandleFullscreenProperty`）。看状态，不要看切换。
  presenter 转换之后**必须**调用 `RefreshAdaptiveState()`。
- **PiP**：独立的无边框置顶窗口，复用 `PlayerControl`。`ExitPiP` 推迟到主窗口显示并完成布局
  之后才定尺寸（否则 `ActualWidth` 是 0）。
- **覆盖操作栏**：活动驱动的可见性，1.5 秒空闲定时器。`ControlPanelGradient` 的 `Height=120`
  只在覆盖模式下成立。
- **控制栏布局**：`ApplyBarOrders` 每次 apply 都从规范列表重建三个 `CommandBar`，
  **绝不**在当前项上过滤。
- **溢出按钮**：三个 `CommandBar` 都设 `OverflowButtonVisibility="Collapsed"`。`CommandBar`
  会在应用自己的 `MoreButton` 旁边画它自己的 `⋯`，没抑制的话会出现两个相邻的溢出按钮，
  其中一个是空的。应用的 `MoreButton` 只在 `ControlBarAdaptiveLayout.HasOverflowContent`
  说菜单里有东西时才出现——那是 `MoreFlyout_Opening` 据以构建菜单的同一张表，
  这就是"不会弹出空菜单"的原因。加一项要同时改 `OverflowEntries`。
- **档位**：`ControlBarAdaptiveLayout.TierFor` 同时接收宽度**和**高度，因为 384 宽的竖窗
  在 `NarrowWidth` 之上，否则永远到不了竖屏档。竖屏保留九个槽位，靠收紧按钮间距（`IsTight`）
  而不是丢控件；唯一例外是 `aspect`，它被移进溢出菜单。这些规则是纯函数并已链接进
  `mpv-conf-test`——跑测试，不要用眼睛验证这一条。
- **播放模式**：单按钮循环 不循环→顺序→单曲循环→随机。隐藏 `RepeatButton`，复用
  `ShuffleButton`。字形 F175 / F172 / EF34 / EF37。
- **弹出层外观**：音量条、轨道选择器、快捷面板和 More 菜单共用一套外观——
  `Styles.xaml` 的 `MpvPlayerPopupPresenterStyle` 与 `MpvPlayerPopupCornerRadius`，外加一个
  **只能写字面量**的阴影深度（`Translation="0,0,32"`，在 `PlayerControl.xaml` 两处），
  因为 `Translation` 是 `Vector3`，XAML 没有它的资源形式。这三处**要一起改**。
  音量条和 More 菜单是 `Flyout`。轨道选择器和控制面板是 `Popup`，
  带 `ShouldConstrainToRootBounds="False"`——`Flyout` 会被窗口裁掉，而这两个必须能出窗。
  它们在自己的 `Border` 上复刻同样的画刷、圆角和 `ThemeShadow`。
  每一个都用 `PlacementTarget` + `DesiredPlacement`/`Placement="Top"` 锚定——
  **绝不**手工计算窗口坐标，那正是把面板放到窗口下方而不是按钮上方的做法。
  因为这两个 `Popup` 不受约束，没有东西会把它们拉回屏幕，所以
  `PlayerControl.ControlPanel.PlacementFor` 读窗口的 `DisplayArea.WorkArea`，
  在居中会让弹出层跑出显示器时把 `Top` 弯成 `TopEdgeAlignedLeft`/`Right`。

---

## 7. 右键菜单

- 从 `dyn_menu.lua` 的 `menu-data` 属性经 `BuildMenuFlyoutFromData()` 构建。
- 条目顺序跟 `input.conf` 的注解顺序（与 mpv-menu-plugin 相同）。
- 无图标、无过滤、无重命名——纯文本渲染。
- 没有 `HiddenMenuTitles` 这层开关了；要隐藏条目就直接改 `input.conf`。
- 原生 C++ 构建器（`MpvMenuBuilder.cpp`）不再参与编译（它在一次 ta.c 金丝雀崩溃后
  被停用，重新启用的路径在 git 历史里）。

---

## 8. 缩略图预览

- 进程内第二个 libmpv 实例（`MpvPreviewer.cpp`），软件渲染。
- Thumbfast 式：异步关键帧 seek，以 `seeking` 标志为闸门；demuxer 缓存（前 32 MiB / 后 16 MiB）；
  `FILE_LOADED` 之后才推迟 seek。
- C# 在 `MediaOpened` 时预热；最小位置增量 0.25 秒。
- 可调：尺寸（180 / 248 / 320 px）与刷新间隔（40–600 ms），都在设置里。

---

## 9. 原生库与架构

`libs\include` 是共享的，但 `libs\lib\<Platform>\` 和 `libs\bin\<Platform>\` **不是**，而且
`$(Platform)` 就是目录名本身。两个工程在目标架构的库缺失时都会拒绝构建
（vcxproj 里的 `CheckNativeLibraries`、csproj 里的 `CheckNativeRuntimeLibraries`）——
这个守卫取代了一次"构建成功但产物里没有播放器"的静默成功。Win32 平台因此被移除；
ARM64 已接线，在等二进制。Windows App SDK 以 2.5.1 元包引用，五个组件用 `ExcludeAssets`
排除——**要一起升**，否则 NU1605 会告诉你为什么。规则与理由见
[`deploy-layout.md`](deploy-layout.md) §2.5。

---

## 10. 命令字符串与顺序

- **命令字符串**：`mpv_command_string` 按 C 风格转义解析。Windows 路径在引号里要写双反斜杠
  （`MpvSettings.cs` 的 `Q()`）。
- **命令顺序**：没有顺序保证。`MpvPlayerPage.xaml.cs` 把
  `MpvSettings.BuildApplyAllCommands()` 当一批发出去，打开路径则由各调用方直接调
  `_mediaPlayer.OpenAsync(...)`。输入转发也是直接调用。
  这里曾经写着"三者都经 `MpvCommandQueue`（一个 FIFO worker）"——**从没有人构造过它**，
  那个类型在本仓库已作废。2026-10-22 更正了这句话。

---

## 11. 鼠标输入

composition 模式下 libmpv 永远收不到 Win32 鼠标输入。应用经输入转发分部类
（`MpvPlayerPage_InputForwarding.cs`）转发键盘与鼠标；显示监控在 `MpvPlayerPage_Display.cs`。
