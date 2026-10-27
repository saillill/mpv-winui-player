# MpvMenuHost 依赖台账（草案，P0/0.1/K11）

> 状态：审查草案，基于 2026-08-07 的 `MpvMenuHost.csproj` 与实测产物。定稿前需随 b1/b2 决策更新。

## 运行期依赖（b2 自绘宿主路径）

| 依赖 | 版本 | 形态 | 用途 | 体积/代价 | 升级路径 | 替代方案 | 结论 |
|---|---|---|---|---|---|---|---|
| .NET | 10（net10.0-windows10.0.26100.0） | 自包含 R2R | 宿主语言运行时 | 计入 266.5MB 产物 | .NET 10 生命周期内升级 | .NET Framework 4.8（系统自带）、纯 Win32/C++ | ❌ 无必要性论证 |
| Windows App SDK | 2.3.1 | 自包含（WindowsAppSDKSelfContained=true，WindowsPackageType=None） | WinUI 3/AppWindow/Acrylic | 同上 | WAS 2.x 补丁/升级 | 系统级 WAS（框架依赖）、Win32（TrackPopupMenu） | ❌ 无必要性论证 |
| WinUI 3 | WAS 2.3.1 内置 | 库 | 菜单 XAML 渲染 | 同上 | 随 WAS | WPF/WinForms（.NET Framework 4.8） | ❌ 无必要性论证 |
| Segoe Fluent/MDL2 字体 | 系统自带 | 字体 | 图标字形 | 0 | 随系统 | 自绘图标 | ✅ 系统组件 |
| user32/dwmapi/shcore/advapi32/kernel32 | 系统自带 | P/Invoke | 窗口/钩子/DPI/主题/注册表 | 0 | 随系统 | — | ✅ 系统组件 |

## 构建期依赖

| 依赖 | 版本 | 用途 | 替代方案 | 结论 |
|---|---|---|---|---|
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2526 | SDK 构建工具 | 系统 SDK | ✅ 常规 |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.5.0 | 未打包 WinUI 运行支持 | — | ✅ 常规 |
| PublishReadyToRun | Release 开启 | 启动提速 | 取消（体积换启动） | ⚠️ 未量化收益 |
| PublishTrimmed | 关闭（有记录原因） | — | WAS 未打包裁剪无法启动（README） | ✅ 已记录 |

## 发布产物实测

| 产物 | 体积 |
|---|---|
| publish-r2r | 267 MB |
| publish-release | 245 MB |
| publish | 221 MB |
| bin / obj | 442 / 104 MB |
| 部署副本（repo tools + 安装目录） | 266.5 MB × 2（518 文件） |

运行代价：每级预热宿主工作集 124–125 MB，三级链 ≈374 MB 常驻。

## b1 原生路径（对照）

| 组件 | 体积 | 运行时依赖 |
|---|---|---|
| menu.dll（tsl0922/mpv-menu-plugin，本地重编译） | 99 KB | 系统 Win32，零新增运行时 |
| dynamic_menu.lua / dyn_menu.lua | 文本 | mpv Lua 环境 |

## 台账结论

1. b2 的两个运行期大依赖（.NET 10、WAS 2.3.1）无书面必要性论证，且存在 b1 零运行时替代——**违反原则 0**。
2. 若保留 b2：优先改框架依赖（系统 WAS 运行时），其次再考虑裁剪；至少应给出“自包含 266.5MB×2 + 374MB 常驻”的收益量化。
3. 台账需随决策定稿后放入宿主仓库 `DEPENDENCIES.md`，并纳入发布校验。
