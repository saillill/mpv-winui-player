# WAS 升级路线研究（2026-08-07）

背景：v4.1 部署后用户反馈“右键菜单点查看就卡死”。上一轮已部署修复（DLL
`ECFC3362`）：叶子菜单不再创建真实 KeyboardAccelerator，只设置
`KeyboardAcceleratorTextOverride` 显示快捷键文本。本轮按计划研究
“升级 Windows App SDK 解决级联菜单卡顿”的可行性，结论如下。

## 1. 升级路线：不可行

- NuGet `Microsoft.WindowsAppSDK` 稳定版最新就是 **2.3.1**（当前工程版本）；
- `2.3.2-experimentala` 为实验通道，`2.2.2-experimental9` 也是实验版，
  生产环境不应采用；
- 因此没有可升级的稳定版本，依赖升级不是本问题的解。

## 2. #9371（级联菜单悬停延迟）：已修复，非本问题

- microsoft-ui-xaml #9371：高刷新/可变刷新率显示器上子菜单偶发延迟数秒；
- 微软确认在 WinUI 3 **1.5.2** 修复，2.3.1 已包含修复；
- 表现是“偶发延迟”，不是“每次点击就卡死”，与本次现象不符。

## 3. #708（非法加速键崩溃/卡死）：仍开放，最可疑根因

- microsoft-ui-xaml #708：`KeyboardAccelerator` 使用非合法 `VirtualKey`
  （逗号、减号、中点等）时 WinUI 在解析加速键文本时崩溃；2019 年确认，
  **截至 2026-07-29 仍开放**；
- 查看子菜单集中了大量非法键文本：`Alt++`、`Alt+-`、`·`、`TAB`、`Del`；
- 微软 Terminal 项目（PR #805）采用 `KeyboardAcceleratorTextOverride`
  显示文本、不创建加速键的规避方案；
- 已核实 WinUI 源码：`MenuFlyoutPresenter::OnItemsSourceChanged` 直接读
  `get_KeyboardAcceleratorTextOverride()` 判断是否有快捷键文本，纯文本覆盖
  足以显示右侧标签；
- 结论：移除真实加速键是正解，已部署。

## 4. #7374（菜单项高度 40 而非 32）：已关闭，暂未采用

- 窗口未被点击过就显示 MenuFlyout 时，菜单项可能以 40px 显示（期望 32px）；
- 2024-03 关闭；H.NotifyIcon 仍用 `Height=32 + Padding` 规避；
- 用户当前未反馈高度异常，暂不改样式。

## 5. 参考实现：H.NotifyIcon SecondWindow（同架构生产代码）

- 透明宿主：`WS_EX_LAYERED` + `LWA_COLORKEY`（黑）+ `LWA_ALPHA=0`，
  子类化把 `WM_ERASEBKGND` 填黑；
- 窗口样式：`WS_POPUPWINDOW`、无边框、置顶、不出现在 Alt-Tab；
- 宿主窗口设 owner（托盘窗口）；
- `FlyoutShowMode.Transient` + `SetForegroundWindow`；
- 我们的实现：1×1 不可见宿主 + `WS_EX_NOACTIVATE` + `Standard` 模式。
  文档明确 `Standard` 是上下文菜单的正确模式（取焦点、支持键盘）；
  在拿到实机证据前不改窗口模式。

## 6. 已否决：子菜单惰性构建

- 把 300+ 项从“启动时全量构建”改成“打开子菜单时再构建”，UI 线程负担
  会摊薄，但 WinUI 对 MenuFlyoutSubItem 的动态增项支持不可靠（只生效一次、
  需二次打开才显示），有引入新闪烁/空白 bug 的风险，暂不实施。

## 7. 诊断准备

- 已临时开启 `host_log=C:\Users\z2637\AppData\Local\Temp\mpv-menu-host.log`
  （仓库与安装目录同步修改，可随时删除该行）；
- 下次实机若仍卡死，日志会停在“FLYOUT shown”之后，可据此定位是
  UI 线程挂起还是 IPC/命令问题。
