# MpvMenuHost 依赖台账

> 状态：随 v4.0 更新（2026-08-07）。方向已定：单进程 WinUI 3 + mpv 组件；依赖最小化仅作后备，不采用额外进程/小内存思路。

## 运行期依赖（当前单进程形态）

| 依赖 | 版本 | 形态 | 用途 | 体积/代价 | 升级路径 | 替代方案 | 结论 |
|---|---|---|---|---|---|---|---|
| .NET | 10（net10.0-windows10.0.26100.0） | 自包含 R2R | 宿主语言运行时 | 计入 266.5MB 产物 | .NET 10 生命周期内升级 | 系统已装运行时/框架依赖（后备方向） | ⚠️ 待架构决策 |
| Windows App SDK | 2.3.1 | 自包含（WindowsPackageType=None） | WinUI 3/AppWindow/Acrylic | 同上 | WAS 2.x 升级 | 系统级 WAS（后备方向） | ⚠️ 待架构决策 |
| WinUI 3 | WAS 2.3.1 内置 | 库 | 菜单渲染 | 同上 | 随 WAS | — | 目标框架，保留 |
| Segoe Fluent/MDL2 字体 | 系统自带 | 字体 | 图标字形 | 0 | 随系统 | 自绘（不采用） | ✅ |
| user32/dwmapi/shcore/advapi32/kernel32 | 系统自带 | P/Invoke | 窗口/钩子/DPI/主题 | 0 | 随系统 | — | ✅ |

## 构建期依赖

| 依赖 | 版本 | 用途 | 结论 |
|---|---|---|---|
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2526 | SDK 构建工具 | ✅ |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.5.0 | 未打包 WinUI 运行支持 | ✅ |
| PublishReadyToRun | Release 开启 | 启动提速 | ⚠️ 未量化 |
| PublishTrimmed | 关闭（WAS 未打包裁剪无法启动，README 记录） | — | ✅ 已记录 |

## 发布产物实测（2026-08-07）

publish-r2r 267MB、publish-release 245MB、publish 221MB、bin 442MB、obj 104MB；部署副本 266.5MB×2（518 文件）。v4.0 后常驻仅 1 个进程（工作集 124–125MB），每级进程链与 trigger/heartbeat/state/RESET 文件协议已删除。

## mpv 本体组件（保留）

input.conf `#menu:` 菜单树、mpv JSON IPC、dyn_menu.lua/dynamic_menu.lua、menu.dll 原生回退（99KB）。

## 台账结论

1. 单进程 WinUI 3 + mpv 组件方案已实施（v4.0）：常驻内存由 3×125MB 降为 1×125MB，进程链/文件协议删除。
2. 自包含 .NET10+WAS 的去留，等方案 A 实机验证结论；届时才评估框架依赖等后备。
