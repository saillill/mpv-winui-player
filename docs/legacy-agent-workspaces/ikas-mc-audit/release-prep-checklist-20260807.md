# mpv-winui-player GitHub 发布准备清单（2026-08-07，第三轮更新）

> 目标：把当前仓库整理成可推送到 GitHub、可由 Actions 构建并发布 Release 编译包的状态。
> 审计详情见 `mpv-winui-player-audit-20260807.md`。

## 1. 决策状态

| # | 决策项 | 状态 | 说明 |
|---|---|---|---|
| 1.1 | 仓库结构：mpv-winui-lazy 并入主仓库 vs 独立仓库 | **已完成：并入主仓库** | 已发布到 saillill/mpv-winui-player（用户授权自行创建） |
| 1.2 | 发布身份与签名 | **已定：不配签名** | 只发 unpackaged zip；包名沿用 `mpv-winui`；MSIX/证书留待以后 |
| 1.3 | input_plus.lua 授权 | **已完成：移除** | 用户确认不需要；脚本、7 条绑定、NOTICES 行已删除（2026-08-07 第十五轮） |
| 1.4 | menu.dll 去留 | **默认保留（已随包）** | WinUI 右键菜单不依赖它，仅 5 条 dialog 菜单项与旧 mpv 回退在用；保留零回归 |
| 1.5 | MediaInfo | **已完成** | 官方 CLI v26.05 随包（BSD-2-Clause），路径 `~~/MediaInfo.exe`，许可证全文已入库 |
| 1.6 | 160 条中文数据菜单 | **已完成** | GitHub 版 `input.conf` 已剥离菜单（86 个真实按键绑定）；本地已备份 `input.conf.bak-20260807-menu`；`menu-audit.md` 移到 `docs/`；数据菜单只剩 dynamic_menu 的动态子菜单 |

## 2. 提交前检查（当前状态）

- [x] `git status` 只含预期文件；无 `bin/`、`obj/`、`packages/`、`libs/`、`dist/`
- [x] `.gitignore` 已含 `/dist/`、`/mpv-winui-lazy/cache/`
- [x] 无硬编码用户路径（`rg "C:\Users|z2637"` 空）
- [x] 无运行数据（deploy 排除 `_cache cache *.log recent.json saved-props.json *.bak*`）
- [x] 日志默认关闭（`log-file` 注释、`hdr_auto` 默认 `log=no`）
- [x] 配置文件 UTF-8 无 BOM
- [x] `mpv-winui-lazy` 含 README / LICENSE / THIRD_PARTY_NOTICES / licenses/MediaInfo-BSD-2-Clause.txt / fonts/OFL-1.1.txt
- [x] dyn_menu.lua 无旧 WinUI 宿主原型与测试钩子；dyn_menu.conf 无过时 `str_*`
- [x] 无 dead 文件（modernz-icons.ttf 已删）
- [x] build.yml：`windows-2025`、pfx/MSIX 条件执行、无 secrets 可跑、产出 Release zip
- [x] package.ps1 `-SkipPublish` 支持；pubxml 已跟踪
- [x] `msg-level=all=info`（修复 `msg-level=info` 非法值）；mpv 配置加载 0 错误 0 警告
- [x] Debug x64 构建 0 警告 0 错误
- [x] 本地化：AppLang 104 属性 ↔ en/zh JSON 104 键完全对齐；XAML 字符串经 `ApplyLocalizedStrings()` 从 AppLang 应用（unpackaged 下 x:Uid/x:Bind 不可用，已改用代码后置；resw 已删除）
- [x] 插件绑定闭环（script-binding/script-message-to 目标与绑点名全部有效）
- [ ] 版本号：`Package.appxmanifest` 与 zip 命名一致（未决）

## 3. GitHub 仓库设置

- **已完成**：仓库 saillill/mpv-winui-player（public），本地 `github` remote 已配置并推送。
- 无证书：无需 secrets；MSIX 步骤自动跳过，产物 = unpackaged 目录 + Release zip。
- 有证书（以后想要 MSIX）：配置 `BASE64_ENCODED_PFX`、`Pfx_Key`。
- workflow 为 `workflow_dispatch` 手动触发；CI 已实测全绿（2026-08-07）。

## 3.1 发布产物

- Release：https://github.com/saillill/mpv-winui-player/releases/tag/v1.0.16
- 附件：`mpv-winui-win-x64-Release.zip`（CI 产物，75.6→77.4MB 区间，内容断言通过）

## 4. 版本号策略（建议）

- `Package.appxmanifest` 的 `Version` 为主版本；`update-package.ps1` 用 `VERSION_BUILD_NUMBER` 递增第四段。
- zip 命名建议：`mpv-winui-win-x64-<版本>.zip`（当前 `mpv-winui-win-x64-Release.zip`）。
- GitHub Release 标题：`v<版本>`，附件 = unpackaged zip。

## 5. 发布流程（无证书最短路径）

1. 本地 `build.ps1 -Configuration Release`（已验证 0 警告 0 错误）。
2. `package.ps1 -Configuration Release` → `dist\mpv-winui-win-x64-Release.zip`（已验证 75.6MB，内容断言通过）。
3. 已本地提交 `082aded`；推送目标仓库后 → Actions 手动触发 → 产物含 unpackaged 目录与 Release zip。
4. 建 GitHub Release，附件 = zip。
5. 用户解压 → 运行 `mpv-winui.exe` → 首次运行前执行 `mpv-winui-lazy\deploy-config.ps1`。

推送命令（用户创建目标仓库后）：

```powershell
git remote add github https://github.com/<你的账号>/<新仓库>.git
git push -u github main
```

然后在 GitHub Actions 页手动运行 build workflow，取 unpackaged / Release zip 产物。

## 6. UI 实机验证清单（待执行）

- [ ] 启动后主窗口正常、菜单栏显示 File/View/Help
- [x] Release 启动冒烟：窗口标题正常、10s 后正常退出、无日志泄漏（2026-08-07 第八轮）
- [x] 语言切换运行时验证：UIA 实测默认英文 `File/View/Help` → `zh-CN` 重启后 `文件/查看/帮助`（2026-08-07 第十二轮；播放列表/更多菜单同属 AppLang 路径，低风险待实机）
- [x] 播放激活修复并验证：unpackaged 下协议/命令行参数到达为 Launch kind，原代码不处理导致无法开文件；已修复（CommandLineCandidates 双来源解析）并经 UIA 实测播放进度推进（2026-08-07 第十三轮）
- [ ] 右键菜单：文件/窗口固定菜单 + VSR/RTX-HDR/着色器动态子菜单（无 160 条数据菜单）
- [ ] 播放视频：HDR 检测（`%LOCALAPPDATA%\mpv-winui\logs\display-info.log`）、RTX HDR/VSR 开关
- [x] MediaInfo 链路：CLI + mpv 脚本端到端实测通过（WAV 样本）
- [ ] 默认不生成 `mpv.log` / `hdr_auto.log`
- [ ] 跨屏拖动窗口：color-kind 跟随显示器
- [ ] 程序目录无写入；本地 160 条菜单备份未被 deploy 删除

## 7. 未决/风险项

| 项 | 状态 | 处理 |
|---|---|---|
| 仓库结构（1.1） | 待拍板 | 推荐并入主仓库 |
| input_plus 保留确认（1.3） | 默认保留 | 保留 + NOTICES 标注；作者回复后再定 |
| menu.dll 保留确认（1.4） | 默认保留 | 影响面已查清；如需移除则删 5 条 dialog 绑定 |
| GitHub Actions 实跑 | 未跑 | 推仓库后验证（本地干净模拟 + 配置冒烟已过） |
| ARM64 | 已定 x64 | libmpv 仅 x64、无 ARM64 工具链 |
| 着色器许可证全文 | 文件头已声明 | 发布前把各许可证文本随包 |
| 跨屏 HDR monitor 字段 | 为空（TODO） | 实机验证时确认 |
