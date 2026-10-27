# mpv-winui-player 发布前审计（2026-08-07 复核）

> 用途：按“部署合规 → 本地化 → 纯净度/插件 → 提示词”四项核查，给 GitHub 发布前的 P0 行动清单。
> 依据：本地仓库、运行目录日志、上游 GitHub 页面与许可证文件逐项查证；旧交接文档仅作线索，凡与本文件冲突处以本文件为准。
> 说明：本文件为审计初稿，可按 doc-coauthoring 流程继续迭代；每条结论尽量附证据路径，标“待实测”的项尚未验证。

## 0. 核查对象与版本

- 参考仓库（fork 基线）：`work\reference\mpv-winui-player`，remote = `ikas-mc/mpv-winui-player`，HEAD `ae57d2f` 与上游 main 一致（`git ls-remote` 已核对）。
- 配置层：`mpv-winui-player\mpv-winui-lazy\`（仓库内未跟踪目录）。
- 运行配置：`%LOCALAPPDATA%\mpv-winui\mpv`（与 mpv-winui-lazy 全量文件 SHA-256 一致）。
- 应用产物：`mpv-winui\mpv-winui\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\`（mpv-winui.dll 19:32，mpv_winrt.dll 17:12）。
- libmpv：`mpv-winui\libs\bin\mpv-2.dll`（58,824,192 B，来自 ikas-mc/mpv-windows-builder 的 release，源码为官方 mpv）。

## 1. 部署合规性与是否“绕弯子”

### 1.1 合规的部分

- C++/WinRT 组件源码未被改动：`mpv-winrt\MpvPlayer.cpp/.h/idl` 均与上游一致；HDR/多显示器功能走的是上游已有的 `DisplayColorKind` + `UpdateDisplayColorInfo` 接口，C# 只调用它。没有“为一个小功能乱改原版组件”。
- 依赖全部来自官方/上游：Microsoft Windows App SDK 2.x 系列包、CsWinRT/CsWin32、Windows SDK BuildTools、CppWinRT、WebView2、NLog；mpv 本体来自官方 mpv 源码构建（ikas-mc/mpv-windows-builder）。
- 配置部署与源码一致（对 mpv-winui-lazy 全部文件逐一比对哈希，无差异）。

### 1.2 不合规 / 绕弯子的部分

1. **构建链路被改成“只认本机 Debug 产物”**（最高优先级问题）
   - `mpv-winui.csproj` 删除了对 `mpv-winrt.vcxproj` 的 `ProjectReference`，改为硬编码引用 `..\bin\x64\Debug\mpv_winrt\mpv_winrt.winmd/.dll`，并手动拷贝 `Microsoft.WindowsAppRuntime.Bootstrap.dll`。
   - 后果：全新 clone 无法构建；Release/ARM64 无对应目录；上游 CI（`.github/workflows/build.yml`：`msbuild /t:Publish` Release x64）在干净环境必然失败。
   - 这不是“官方组件问题”，而是本地为了“dotnet 构建时不用重编 C++”绕的弯子，发布前必须改回可复现构建（见第 8 节 P0-1）。
2. **vcxproj 改动缺少说明**
   - 移除了 `ApplicationType=Windows Store`、强制 `stdcpp20`、改 OutDir/IntDir、显式引用 WebView2 winmd。这些可能是本地 VS Build Tools 环境所需，但未写入交接/文档，无法判断哪些是必要的；发布仓库时应尽量贴近上游，或逐条注明原因。
3. **开发身份残留**
   - `Package.appxmanifest` 仍是 `mpv-winui-dev` / `CN=ikas-mc-dev`（上游开发身份），发布前需替换为正式包名/发布者，或走未打包 zip + 无签名说明。

## 2. 残留物与发布卫生

### 2.1 仓库内（会随 GitHub 发布的内容）

- `mpv-winui-lazy/` 未跟踪且**未被 .gitignore 覆盖**：`git check-ignore` 无输出。发布时要么作为子仓库/独立仓库，要么显式纳入并补文档。
- `mpv-winui-lazy/script-opts/dyn_menu.conf` 残留旧“WinUI 菜单宿主”死配置：
  - `host_menu_path=~~\tools\winui-menu-host\MpvMenuHost.exe`（源码里 tools/ 已删除）；
  - `host_log=C:\Users\z2637\AppData\Local\Temp\mpv-menu-host.log`（**硬编码用户绝对路径**，发布前必须删除/注释）。
- `mpv-winui-lazy/` 内**没有 LICENSE/README/THIRD_PARTY_NOTICES**：字体（30MB）、着色器、脚本、两个二进制 DLL 的来源与许可证未随包说明。
- 新增 `IconMap.cs` 使用系统 Segoe Fluent/MDL2 字体，无外部依赖，合规；但注释为中文且文件编码在 git 中显示为乱码风险（建议统一 UTF-8 无 BOM）。

### 2.2 运行目录（%LOCALAPPDATA%\mpv-winui\mpv）

- 残留空目录 `tools\`（旧菜单宿主目录，deploy 脚本用 robocopy `/E` 不删除旧项，应改 `/MIR` 或显式清理）。
- `hdr_auto.log` 330KB 是“日志门控”修复前的历史文件；`mpv.log` 77KB+（`msg-level=all=v` 全量 verbose）；`recent.json`、`_cache\watch_later` 为运行隐私数据。发布版本应默认降级日志级别、日志移出配置目录或仅调试开启。
- 应用把 `display-info.log` 写到**程序目录**（`AppContext.BaseDirectory`）：本机 Debug 可用，发布到受保护目录会静默失败；应移到 `%LOCALAPPDATA%\mpv-winui\logs` 或删除。

### 2.3 工作区（不会进 git，但建议归档）

- `work\` 下残留大量测试脚本与日志：`analyze_frames.ps1`、`capture_flash.ps1`、`probe*.ps1`、`debug_rightclick_ipc.ps1`、`mpv-dbg.log`（595KB）、`build-check-out*` 等。不影响发布，但建议移到 `work\scratch\` 或归档，避免误导后续对话。

## 3. 本地化适配度（重点偏差）

### 3.1 现状

- 原项目本身只有“语言”骨架：`AppLang.cs` 默认英文、`AppSettings.CurrentLanguage` 存在但无人消费；没有 .resw / x:Uid / 语言 JSON；设置页标签仍是英文硬编码；清单 `Resources` 为 `x-generate`，但没有可生成的资源变体。
- 上一轮把 `AppLang.cs` 默认值和多处 XAML/代码菜单**硬编码改成中文**（如 Play→播放、Open File→打开文件），导致：
  - UI 变成“中英混杂”（设置页 Theme/Backdrop/Debug Log 仍英文）；
  - 原语言选择/导入/导出机制被绕过且未实现，反而更不满足“支持所有语言”；
  - 对非中文用户不友好，也不符合原项目默认英文的基线。
- mpv 本身没有 UI 本地化（OSD 为英文）；mpv-lazy 配置的菜单/OSD 字符串是中文（`dyn_menu.conf` 的 `str_*`、`input.conf` 的 `#menu:` 注释、`dynamic_menu.lua` 标题）。

### 3.2 建议（P0-2）

- 回滚硬编码中文：`AppLang.cs` 默认恢复英文，XAML 与 `MpvPlayerPage_MpvMenu.cs` 的文本改走资源。
- 二选一实现真正本地化：
  1. WinUI 标准 `x:Uid` + `Strings\en-US\*.resw` / `zh-CN\*.resw`（发布 MSIX 时自动生成语言目录）；
  2. 补完现有 AppLang JSON 加载器（复用 `CurrentLanguage` 与“导入/导出语言”入口），默认英文，附带 `zh-CN.json`。
- 菜单层：`input.conf` 的 `#menu:` 标题与 `dyn_menu.conf` 的 `str_*` 按配置覆盖（英文默认 + 中文覆盖文件），并在 README 说明如何切换。

## 4. 纯净度与插件核查

### 4.1 已清理项（相对旧 mpv-config）

- ModernZ（modernz.lua + modernz*.conf）、k7f_zen、旧 winui-menu-host 工具目录已从源码移除；`#menu:` 菜单 195→160 条已核实（`rg -c '#menu:'` = 160）。

### 4.2 仍偏重、需决策

- 当前配置包含 20 个脚本、2 个二进制 DLL、30MB 字体、18 个着色器、17 个 VapourSynth 脚本。作为“懒人增强层”可接受，但若目标是“最小纯净 mpv”，应把可选项移入 `profiles`/`scripts-opt` 白名单或拆成可选包，并写明哪些默认加载。
- `menu.dll` 与 `darkmode.dll`：`dyn_menu.conf` 已是 `use_mpv_impl=yes` + `host_menu=no`，mpv 原生菜单 API 生效，Win32 菜单链路大概率已不被使用（**待实测**：移除两个 DLL 后菜单/文件对话框是否正常）。若确认无用，删除可显著减体积并减少 GPL 二进制。

### 4.3 许可证（GitHub 发布硬门槛）

- `menu.dll` / `dyn_menu.lua` / `dialog.lua` 来自 tsl0922/mpv-menu-plugin，SPDX 头为 **GPL-2.0-only**；GPL 二进制需要随包提供源码或指向源码 + 本地补丁。
- mpv 自带脚本（select/console/stats 等）为 LGPL-2.1+/ISC，头文件已带声明，OK。
- hooke007/mpv_PlayKit 的 LICENSE.MD 明确“未列出的文件默认 UNLICENSED”——`mpv.conf/profiles.conf/input.conf/input_plus.lua` 等改编自 mpv-lazy 的文件需要保留来源声明，或改写为自创内容。
- 字体：Source Han Sans / LXGW WenKai 为 OFL-1.1，modernz-icons.ttf 属 ModernZ（LGPL-2.1），均需附许可证文本。
- 着色器许可证在各文件头，建议随包放 `THIRD_PARTY_NOTICES.md` 汇总。
- 仓库根 LICENSE.txt 是上游 app 的 LGPL-2.1；配置包应独立声明，不能简单共用同一许可证。

## 5. 功能可运行性（现有证据）

- 构建产物齐全且时间合理：`mpv-winui.dll` 19:32（含 19:32 的 Display 修复）、`mpv_winrt.dll` 17:12、页面 XBF 17:42（与中文 XAML 修改时间匹配）。
- 应用日志显示菜单点击命令实际执行成功：hdr_auto 模式切换、console/stats、章节跳转、截屏等（`%LOCALAPPDATA%\mpv-winui\logs\mpv-winui.2026-08-07.log.txt`）。
- `mpv.log` 显示全部脚本加载、`d3d11-composition-size` 设置、正常 quit，无崩溃栈。
- `display-info.log` 显示 HDR 检测正确（kind=HDR、maxLuma=1000、sdrWhite=480），但 `monitor=` 字段为空（已知 TODO，跨屏验证未完成）。
- **未做**：本次未启动 GUI（遵循你不做动态 UI 测试的偏好）；Release/ARM64/干净 clone 构建未验证（第 1.2 节已指出必然失败点）。

## 6. 旧交接文档的出入点

- “C++ 组件已有现成产物、无需重编”：对本机成立，但文档未说明代价是仓库不可复现构建，属于误导。
- “hdr_auto.log 已门控”：代码已门控，但历史 330KB 文件仍在运行目录，未清理。
- “配置已同步部署”：哈希比对确认一致。
- “菜单 195→160”：核实一致。
- “display-info.log monitor 字段为空”：核实一致。
- “mpv-winui-lazy 未跟踪”：核实一致，且未被 .gitignore 覆盖，发布策略缺失。

## 7. 提示词改进建议（针对你这条/这类提示词）

1. 一次只验收一个主题：把“部署合规、本地化、纯净度、插件”拆成 4 个独立任务，每个带 P0 验收标准，避免四个主题一起浅查。
2. 明确“原版”和“语言”的定义：是 ikas-mc/mpv-winui-player、mpv-player/mpv 还是 hooke007/mpv_PlayKit？“支持所有语言”要可测（例如：en-US/zh-CN 资源齐全、切换后无硬编码残留）。
3. 给“最小纯净度”量化边界：列出必须保留的脚本/字体/着色器清单，删除项给理由，否则无法验收。
4. 写清发布目标：GitHub 仓库是“源码可复现构建”还是“Release 编译包”？两者对构建链路的要求不同。
5. 要求先证据后结论：每条结论附文件路径/行号/日志时间戳；对旧交接文档先标注“已核实/未核实”。
6. 明确动作边界：可以改哪些文件、部署/清理运行目录是否需要确认，避免后续对话越权。
7. “先搜网络”可以更具体：要求先列出候选官方方案并附链接（WinUI 资源系统、Windows App SDK 文档、mpv 官方接口、libplacebo issue），再决定是否写代码。
8. 验收要“能运行”证据：给出启动命令、日志路径、最小测试媒体；仅“构建成功”不算完成。

## 8. P0 行动清单（建议下一步，按序执行）

- P0-1 恢复可复现构建：csproj 改回 ProjectReference（或正式的多项目 publish 流程），确保全新 clone 下 `msbuild ... /t:Publish` Release x64 通过；vcxproj 的必要改动逐条注释说明。
- P0-2 本地化：回滚硬编码中文 → x:Uid/.resw 或 AppLang JSON；默认英文，附 zh-CN；清理 `AppSettings.CurrentLanguage` 死代码或实现它。
- P0-3 发布卫生：删除 dyn_menu.conf 死配置与用户绝对路径；`display-info.log`/`hdr_auto.log` 移入 %LOCALAPPDATA% 或改调试开关；`msg-level=all=v` 降级；deploy 改 /MIR 或显式清理；确认 menu.dll/darkmode.dll 是否可移除。
- P0-4 许可证：新增 `mpv-winui-lazy/LICENSE`、`THIRD_PARTY_NOTICES.md`、字体/着色器许可证文本、GPL 二进制源码链接与补丁说明。
- P0-5 发布身份与打包：正式 Package 身份/发布者、版本号策略、MSIX + unpackaged zip 两种产物说明。
- P0-6 实测清单：跨屏 HDR 切换、移除 menu.dll 后回归、Release/ARM64 构建、干净 clone 构建、日志不再写入程序目录。

## 9. P0 执行记录（2026-08-07 晚，已动手的部分）

> 以下改动已实际完成并验证（本地 Debug/Release x64 构建均 0 警告 0 错误）。

### 9.1 P0-1 构建链（已完成）

- `mpv-winui.csproj`：改为双路径——
  - 默认（本地）：引用 `..\bin\$(Platform)\$(Configuration)\mpv_winrt\` 下已编译产物（winmd/dll/Bootstrap.dll）；
  - `-p:BuildMpvWinrtWithReference=true`（CI）：恢复上游 `ProjectReference`，由 msbuild 联动编译 C++ 组件。
- 新增仓库根 `build.ps1`：先 VS MSBuild 编 `mpv-winrt.vcxproj`，再 `dotnet build` 编 C# 应用；已用 Debug/Release x64 各跑通一遍。
- `.github/workflows/build.yml`：两个 msbuild 步骤加入 `/p:BuildMpvWinrtWithReference=true`，保持上游 CI 行为。
- 说明：`dotnet build` 无法直接编译 C++ 项目（`$(VCTargetsPath)` 属于 VS 组件），这正是上一轮改硬编码引用的根因；现在本地与 CI 两条路径都自洽。

### 9.2 P0-2 本地化（基本完成，待 UI 实测）

- `AppLang.cs` 恢复英文默认，新增菜单/设置字符串属性与 `LoadFromJson()`（带 `DynamicallyAccessedMembers` 标注，兼容 PublishAot/Trimmed）。
- `AppContext.Init()` 按 `AppSettings.CurrentLanguage`（默认 en-US）加载 `%LOCALAPPDATA%\mpv-winui\languages\<lang>.json` 或程序目录 `Languages\<lang>.json`。
- 新增 `Languages\en-US.json`、`Languages\zh-CN.json`（随构建复制到输出目录）。
- XAML 菜单恢复英文并挂 `x:Uid`；新增 `Strings\en-US\Resources.resw`、`Strings\zh-CN\Resources.resw`（播放列表右键、菜单栏、控制条 More 菜单、设置标题全覆盖）。
- `MpvPlayerPage_MpvMenu.cs` 代码创建的菜单改走 `AppContext.AppLang`（保留 IconMap 图标与 `&&` 还原逻辑）。
- 设置页新增“界面语言”选项（en-US / zh-CN，保存到 CurrentLanguage，重启生效），并本地化 Theme/Backdrop/Debug Log 标签。
- 构建后 `resources.pri` 已包含 en-US 与 zh-CN 资源及 `PlaylistPlay` 等键；并同时输出 `mpv-winui.pri`（兼容不同 Windows App SDK 版本的未打包 PRI 查找名）；XBF 由 PRI 内嵌，符合未打包 WinUI 3 布局。

### 9.3 P0-3 发布卫生（已完成）

- `dyn_menu.conf`：删除旧 WinUI 菜单宿主死配置与硬编码用户路径 `host_log=C:\Users\z2637\...`。
- `deploy-config.ps1`：robocopy 改 `/MIR`，排除 `_cache`、`cache`、`*.log`、`recent.json`、`saved-props.json`（运行数据不部署、不删除）。
- `mpv.conf`：`msg-level=all=v` 改为 `info`（注释保留调试用法），控制 mpv.log 体积。
- `MpvPlayerPage_Display.cs`：`display-info.log` 改写到 `%LOCALAPPDATA%\mpv-winui\logs\`，不再污染程序目录。
- 运行目录残留的空 `tools\` 目录已删除（robocopy /MIR 对空目录未自动清理，属一次性清理；如复现再处理）。

### 9.4 P0-4 许可证（初版完成）

- 新增 `mpv-winui-lazy\README.md`、`LICENSE.md`、`THIRD_PARTY_NOTICES.md`（自创脚本 LGPL-2.1-or-later；第三方组件逐项列出来源与许可证，含 GPL-2.0-only 的 menu.dll/dyn_menu/dialog、mpv-lazy 的 UNLICENSED 说明、OFL/LGPL 字体）。
- 发布前仍需：把字体 OFL 全文与着色器许可证文本复制进包；确认 input_plus.lua 等无 SPDX 头文件的上游授权；darkmode.dll/menu.dll 的源码或补丁链接。

### 9.5 仍未完成/待实测

- GitHub Actions 真实 CI 运行（本地已做“干净源码模拟”，尚未在 GitHub 上跑）。
- ARM64 与 `msbuild /t:Publish`（VS MSBuild）产物未验证。
- UI 实测：语言切换、x:Uid 生效、菜单显示、PRI 加载。
- `menu.dll`/`darkmode.dll` 是否可移除（`use_mpv_impl=yes` 后大概率无用）。
- 项目级 README 仍是上游原样，未说明本 fork 的构建/配置/发布方式。
- Package 身份（`mpv-winui-dev`）、发布者、版本号策略、MSIX + zip 发布脚本。
- 跨屏 HDR 实机验证（monitor 字段仍为空）。

### 9.6 发布链路验证（2026-08-07 晚第二轮）

- `dotnet publish -c Release -p:Platform=x64`（AOT 原生编译）成功：输出 `mpv-winui\mpv-winui\bin\win-x64\publish\`，约 211MB（含 mpv-winui.exe ≈12MB、mpv_winrt.dll、mpv-2.dll、Bootstrap、`resources.pri` 与 `mpv-winui.pri` 双名、Languages JSON）。
- 新增 `package.ps1`：publish 后剔除 *.pdb，附加 `mpv-winui-lazy` 配置层，产出 `dist\mpv-winui-win-x64-Release.zip`（72.1MB，317 项，无空 tools 目录）。
- 干净源码模拟构建通过：`work\clean-sim` 仅含源码 → 下载 nuget.exe + mpv-2.dll.zip → `nuget restore`（packages.config 10 包）→ `build.ps1 -Debug x64` 0 警告 0 错误。该流程与 CI 的“还原→下载 libs→构建”一致。
- 许可证查证更新：thumbfast=MPL-2.0；mpv-coverart / recent-menu / metadata-osd = MIT；input_plus.lua 无 SPDX 头（mpv_PlayKit 默认 UNLICENSED，发布前需确认或移除）；`fonts/OFL-1.1.txt` 已入库，OFL 字体许可证合规项完成。
- 源码中残留的空 `mpv-winui-lazy\tools\` 目录已删除。

### 9.7 发布打包修正（2026-08-07 晚第三轮）

- 发现 `*.pubxml` 被 .gitignore 忽略，干净 clone 中不存在发布配置文件，`package.ps1` 原先依赖的 `bin\win-x64\publish` 会失效；已改为在脚本内显式传 `-p:PublishDir=`（绝对路径+结尾分隔符），不再依赖被忽略的 pubxml。
- `.gitignore` 新增 `/dist/` 与 `/mpv-winui-lazy/cache/`，防止打包产物与运行缓存被提交。
- `package.ps1` 重新验证通过，`dist\mpv-winui-win-x64-Release.zip`（72.1MB）正常生成。
- 新增发布准备清单：`release-prep-checklist-20260807.md`（仓库结构决策、身份/签名决策、提交前检查、Actions secrets、版本策略、UI 实机验证清单、未决风险表）。

### 9.8 日志行为实测与默认关闭（2026-08-07 晚第四轮）

- 冒烟测试（Release publish exe）：启动成功（窗口标题 “mpv winui”），存活 15s 后经 CloseMainWindow 正常退出；`display-info.log` 写入 `%LOCALAPPDATA%\mpv-winui\logs\`（kind=HDR/maxLuma=1000 检测正常），程序目录不再产生该文件。
- 发现并实测 mpv 行为：只要启用 `--log-file`，mpv 就会把日志级别强制提到 verbose，且 `--msg-level` **无法**把它压低（mpv issue #7774，手册已注明）。因此仅改 msg-level 不能控制 mpv.log 体积。
- 发布默认改为：`mpv.conf` 注释掉 `log-file`（排障时取消注释并临时 `msg-level=all=v`）；`hdr_auto.lua` 新增 `log` 选项默认 false，`script-opts/hdr_auto.conf` 提供 `log=no` 开关。
- 第二次冒烟确认：应用正常启动/退出，`mpv.log` 与 `hdr_auto.log` 均不再生成；运行目录里旧的两个日志文件已删除。
- 临时模拟目录与下载文件（clean-sim、nuget.exe、mpv-2.dll.zip）已清理。

### 9.9 插件收尾与平台边界（2026-08-07 晚第五轮）

- `darkmode.dll` 已删除：静态核查确认无任何代码/配置引用（console.lua 已改为 LuaJIT FFI 直读注册表且注释明确“不依赖 darkmode.dll”）；运行目录与发布 zip 均已同步移除。
- `menu.dll` 暂保留：dyn_menu.lua 与 dialog.lua 仍有 `menu_native` 回退路径（文件对话框/剪贴板消息依赖菜单实现），移除需做一次文件对话框回归，列入待实测。
- ARM64 结论：本机无 MSVC ARM64 工具链（`bin\Hostx64\arm64` 不存在），且 libmpv 仅提供 x64（mpv-windows-builder 只出 x64），上游 CI 也只构建 x64——发布范围确定为 **Windows x64 单架构**。
- 本机 VS MSBuild 无法解析 .NET SDK（`Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` 缺失），因此“`msbuild /t:Publish` + ProjectReference”这条 GitHub Actions 路径只能由 GitHub runner 实测；本地已用等价的 `dotnet publish`（AOT）+ 干净源码模拟覆盖。

### 9.10 菜单链路查证与发布卫生收尾（2026-08-07 第六轮）

- **菜单渲染链路最终确认**：`MpvPlayerPage_MpvMenu.cs` 的 `PlayerView_ContextRequested` → `MpvMediaPlayer.MenuData()` → C++ `MpvPlayer::GetMenu()` 直接读 mpv `menu-data` 属性，在 WinUI MenuFlyout 渲染；右键菜单**不经过 menu.dll**。menu.dll 仅剩两个用途：`dialog.lua` 的 5 条 `script-message-to dialog`（加载视频/音频/字幕、复制路径/元数据、导出播放列表）与 mpv <0.38 回退。
- **dyn_menu.lua 清理**：删除旧 WinUI 菜单宿主原型（`host_menu`/`host_menu_path`/`host_test_select`/`host_log`/`launch_host_menu` 及 JSON/管道代码）——这些是上一轮遗留的测试钩子；已用 mpv 0.41.0-615 实载验证（日志 `[dyn_menu] commit menu items: menu-data`，无 Lua 错误）。`script-opts/dyn_menu.conf` 同步删除过时 `str_*` 与“本地重编译版”说明。
- **input_plus.lua 授权查证**：discussions/615 仅为功能列表、无授权声明；mpv_PlayKit LICENSE.MD 未列出文件默认 UNLICENSED；THIRD_PARTY_NOTICES 已如实标注并建议发布前向上游确认。
- **死文件清理**：`fonts/modernz-icons.ttf` 已删（ModernZ 已移除，全仓库无引用；README/NOTICES 同步）；运行目录已随 deploy-config.ps1 `/MIR` 同步删除。
- **发布工作流修正**：`runs-on` 由测试标签 `windows-2025-vs2026` 改为正式 `windows-2025`（官方 changelog 确认 2026-06-15 后该测试标签已指向同一镜像，且明确“仅测试用”）；pfx 解码/MSIX 打包/上传步骤改为 `if: secrets.BASE64_ENCODED_PFX != '' && secrets.Pfx_Key != ''` 条件执行，**无 secrets 的新仓库也能跑通并产出 unpackaged Release zip**；Create Unpackaged 显式传 `GenerateAppxPackageOnBuild=false`、`AppxPackageSigningEnabled=false`；新增 `Create Release Zip` 步骤复用 `package.ps1 -SkipPublish`（已加该参数），上传 `dist\mpv-winui-win-x64-Release.zip`。
- **pubxml 复查**：`Properties/PublishProfiles/win-x64.pubxml`、`win-arm64.pubxml` 确认被 git 跟踪（此前“被 .gitignore 忽略导致 CI 缺文件”的判断不成立；本地打包改显式 PublishDir 仍保留，双保险）。
- **新发现的缺口——MediaInfo**：`工具 > 显示/复制 MediaInfo 信息` 依赖 `~~/../MediaInfo.exe`（`script-opts/stats_mediainfo.conf`），发布包与运行目录均未包含，当前会静默失败；选项：随包附官方 MediaInfo CLI 或删除相关菜单项/脚本（已列入决策清单 1.5）。
- **数据菜单语言**：App 界面 en/zh 可切换；160 条数据菜单文本来自 mpv-lazy `input.conf`（中文），不随语言变化；根 README 已如实说明（决策清单 1.6）。
- **旧交接文档**：`mpv-winui-player-handoff-20260807.md` 已加“已过时”更正横幅，指向本文件与发布清单。
- 最终提交前扫描：`rg "z2637|C:\Users"` 无命中；`git add -A -n` 清单 92 项（无 bin/obj/publish/dist/libs/packages/cache）；build.ps1/package.ps1/deploy-config.ps1 语法校验通过；build.yml YAML 解析通过。

### 9.11 用户决策落实与补充审计（2026-08-07 第七轮）

- **签名/身份**：用户明确“不配签名”，发布走 unpackaged zip；包名沿用 `mpv-winui`（发布身份留待后续可选）。
- **MediaInfo 随包（决策 1.5 落实）**：下载官方 MediaInfo CLI v26.05（mediaarea.net，Windows x64 zip）验证可独立运行，SHA-256 `30F2828A...D89`，BSD-2-Clause；`MediaInfo.exe`（9.3MB）放入 `mpv-winui-lazy/`，许可证全文放 `licenses/MediaInfo-BSD-2-Clause.txt`；`script-opts.conf` 与 `stats_mediainfo.conf` 的路径由 `~~/../MediaInfo.exe` 改为 `~~/MediaInfo.exe`（随配置层一起部署）；THIRD_PARTY_NOTICES 已记录版本与哈希。
- **GitHub 版去除 160 条中文数据菜单（决策 1.6 落实）**：仓库 `input.conf` 剥离全部 `#menu:`/`#@` 注释与 `_` 菜单占位行（207→95 行，86 个真实按键绑定，mpv 解析通过）；本地 160 条菜单版已备份为 `%LOCALAPPDATA%\mpv-winui\mpv\input.conf.bak-20260807-menu`，`deploy-config.ps1` 新增 `*.bak*` 排除保护；`menu-audit.md` 移出配置层到仓库 `docs/menu-audit-20260807.md`（不进 zip）；右键数据菜单只剩 `dynamic_menu.lua` 生成的 VSR/RTX-HDR/着色器动态子菜单。
- **本地化静态审计通过**：44 个 `x:Uid` 与 en/zh `Resources.resw` 键完全闭合、无孤儿键；`AppLang` 60 个 JSON 键在 en/zh 完全一致、无空值（`AppName`/`AppVersion` 为代码内计算属性，无需 JSON 键）。
- **插件绑定闭环审计通过**：`input.conf` 的 `script-binding`/`script-message-to` 目标脚本（console/select/stats/stats_mediainfo/input_plus/dialog/save_global_props）全部存在于 `scripts/`，且每个绑点名均在对应脚本注册（含 `select/menu`、`select/show-properties`）；`dynamic_menu.lua` 无 k7f/RIFE 残留引用；`script-opts/*.conf` 与脚本一一对应。
- **mpv.conf 冒烟发现并修复**：`msg-level=info` 是非法取值（mpv 要求 `<前缀>=<级别>`，裸 `info` 解析失败，日志实锤 `mpv.conf:14 setting option msg-level='info' failed`），已改为 `all=info`；复跑配置加载 0 错误 0 警告，`input.conf parsed: 86 binds`。
- **构建验证**：`build.ps1 -Debug x64` 0 警告 0 错误（C++ mpv_winrt + C# mpv-winui）。
- 提交预演：94 项（含 MediaInfo.exe、licenses/、docs/、剥离后的 input.conf），无构建产物/运行数据。
- 仍待办：仓库结构决策（1.1）、input_plus 保留确认（1.3）、menu.dll 保留确认（1.4）、UI 实机回归（语言切换/右键菜单/播放/跨屏/MediaInfo 菜单项）、GitHub 推送后 CI 实测。

### 9.12 Release 打包与运行级验证（2026-08-07 第八轮）

- **打包链路复验**：`build.ps1 -Release x64` 0 警告 0 错误；`package.ps1`（AOT publish）产出 `dist\mpv-winui-win-x64-Release.zip` 75.6MB。
- **修复 zip 路径分隔符**：PowerShell 5.1 `Compress-Archive` 写入反斜杠条目名（非标准），已改为显式 `ZipArchive` 逐条目写入（正斜杠）；重打后 316 条目、0 个反斜杠路径，`Expand-Archive` 解压与 `MediaInfo.exe` 运行均正常。`package.ps1 -SkipPublish` 路径同步验证。
- **zip 内容断言**：`mpv-winui-lazy/MediaInfo.exe` 与 `licenses/MediaInfo-BSD-2-Clause.txt` 在位；`input.conf` 内 0 条 `#menu:`；无 `menu-audit`、无 PDB、无 cache。
- **MediaInfo 端到端实测**：生成 1 秒 WAV → MediaInfo CLI `--Output=JSON` 解析成功（General+Audio）；通过 mpv IPC 触发 `stats_mediainfo/display_toggle`、`refresh`，日志确认 `~~/MediaInfo.exe` 解析到 `%LOCALAPPDATA%\mpv-winui\mpv\MediaInfo.exe`、subprocess 启动成功、无 [e] 错误（首次尝试用 `--frames=1` 导致 mpv 提前退出杀子进程，属测试方式问题，去掉帧限制后通过）。
- **Release 启动冒烟（新配置层）**：窗口标题 “mpv winui”，运行 10s 后 `CloseMainWindow` 退出码 0；默认不生成 `mpv.log`/`hdr_auto.log`；`%LOCALAPPDATA%\mpv-winui\logs\display-info.log` 正常写入（kind=HDR、maxLuma=1000；`monitor=` 字段仍为空，列入实机 TODO）。
- 临时测试文件（WAV/日志/解压目录）已清理；提交预演仍为 94 项。

### 9.13 本地化机制缺口与修复（2026-08-07 第九轮）

- **发现缺口**：此前 XAML 用 `x:Uid` + resw（en/zh），但 unpackaged WinUI 3 中 `PrimaryLanguageOverride` 不受支持、`ResourceContext.SetGlobalQualifierValue("Language", ...)` 在 Win11 可能崩溃且不影响 x:Uid（官方 WindowsAppSDK #3052、microsoft-ui-xaml #10430 实锤）——设置页的语言选项只能切换 AppLang 代码菜单，XAML 字符串不会跟随，属于半成品功能。
- **尝试 x:Bind 静态属性失败**：把 44 处 XAML 改为 `{x:Bind app:AppContext.AppLang.X}` 编译通过，但 Release 启动即崩溃（0xC000027B，Microsoft.UI.Xaml.dll，WER 记录 combase 80004003）——WinUI 运行时对静态属性 x:Bind 支持不可用。
- **最终方案（已实现并验证）**：XAML 恢复硬编码英文默认值，新增 `ApplyLocalizedStrings()` 在页面构造后从 AppLang 赋值（PlaylistContextMenu 按 Tag 映射、MenuBar 按 Tag/子项 Tag 映射、PlayerControl 按 x:Name、Settings 标题直接赋值）；AppLang 补齐 44 个字符串属性（en 默认值与旧 resw 一致），en/zh JSON 各 104 键且与属性完全对齐；删除不再使用的 `Strings/*.resw`。`AppContext.Init()` 在 `App()` 构造时执行（早于窗口创建），语言 JSON 先于页面加载。
- **验证**：Debug/Release 构建 0 警告 0 错误；Release 启动冒烟窗口正常、退出码 0（崩溃已消除）；PRI/资源不再承载 UI 文案，单一来源 = AppLang + JSON。文本级显示验证仍列入 UI 实机清单。

### 9.14 提交固化（2026-08-07 第十轮）

- 提交 `082aded`（main，领先 origin 1 个提交）：95 文件，+32743/-75；工作区干净。
- 提交内容：可复现构建/打包脚本、CI 无 secrets 支持、AppLang 本地化（104 键）、MediaInfo 随包、配置层清理（剥离中文菜单、移除测试钩子、修复 msg-level）、许可证与文档。
- 终检：AppLang 带 `[DynamicallyAccessedMembers(PublicProperties)]`（AOT/裁剪安全）；publish 与 zip 内 en/zh JSON 各 104 键；zip 316 条目全正斜杠、无 PDB；75.6MB。
- 推送前需用户新建/指定目标仓库（当前 remote 仍是上游 ikas-mc/mpv-winui-player，不可直接推）；若选“独立仓库”方案 B，可把 `mpv-winui-lazy/` 拆到新仓库（历史可保留）。

### 9.15 提交态干净克隆构建验证（2026-08-07 第十一轮）

- 从提交 `082aded` 克隆到临时目录 → 下载 mpv-2.dll.zip（ikas-mc/mpv-windows-builder 20260731）解压到 `mpv-winui/libs` → 下载 nuget.exe → `build.ps1 -Debug x64`：C++/C# 均 0 警告 0 错误，用时约 40s。证明提交自包含、可复现（与 CI 的还原→下载 libs→构建路径一致）。
- 提交树卫生：`git ls-tree HEAD` 无 bin/obj/dist/publish/packages/libs、无 pdb/log/cache 条目；CI 引用的 9 个文件（workflow、两个 pubxml、脚本等）全部在提交树内。
- 临时模拟目录清理时被系统索引短暂占用（仅 TEMP 内残留，不影响仓库）。

### 9.16 语言切换运行时验证（2026-08-07 第十二轮）

- 用 Windows UI Automation（无障碍接口，无输入注入）读取 Release 应用实际渲染的菜单文本：
  - 默认（注册表 `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app` 无 CurrentLanguage）：菜单栏 = `File / View / Help`；
  - 设置 `CurrentLanguage=zh-CN` 后重启：菜单栏 = `文件 / 查看 / 帮助`；
  - 测试后已还原注册表值。
- 证明链路成立：注册表设置 → `AppSettings.CurrentLanguage` → `AppContext.LoadLanguage` → `AppLang.LoadFromJson` → `ApplyLocalizedStrings` → 实际渲染文本。en/zh 双向验证通过。
- 仍待实机确认：播放列表右键菜单、播放控制“更多”菜单、设置页（同属 AppLang 代码路径，风险低）、播放/跨屏 HDR、右键动态菜单。

### 9.17 播放激活修复与端到端验证（2026-08-07 第十三轮）

- **发现真实 bug**：unpackaged 模式下 `mpv-winui://?file=` 协议与命令行参数激活到达时 `AppInstance` 上报 `Kind=Launch`（Data 为原始命令行，首 token 是 exe；`Environment.GetCommandLineArgs()[0]` 是 dll），而 `ActivationService` 只处理 `File/Protocol` 两种 Kind——`Parse`/`ParseFileItemsAsync` 直接返回空，应用开文件功能（含协议）实际不可用。用临时诊断日志（已移除）实锤：`kind=Launch dataType=WinRT.IInspectable cmd=...mpv-winui.dll <path>`。
- **修复**：新增 `CommandLineCandidates()`，从 `GetCommandLineArgs()[1..]` 与 `ILaunchActivatedEventArgs.Arguments`（原始命令行，跳过首 token、去重）收集候选路径/URI；`Launch` 分支解析 `mpv-winui:` URI、存在文件/目录；`Parse` 与 `ParseFileItemsAsync` 同步处理。
- **验证（UIA 读取时间文本）**：直接命令行 `mpv-winui.exe <30s wav>` 与协议 `mpv-winui://?file=...` 均起播：T+3s=`00:02/00:30` → T+7s=`00:06/00:30`（进度前进、时长正确）。测试用协议注册与 EnableDebugLog 已还原，临时文件已清理。
- Release 重建（75.8MB）+ 启动冒烟通过（退出码 0）。

### 9.18 Release 正式产物播放验证（2026-08-07 第十四轮）

- 用最终 Release publish（AOT）产物复测：命令行 `mpv-winui.exe <30s wav>` 与协议 `mpv-winui://?file=...` 均起播（T+3s=`00:02/00:30` → T+7s=`00:06/00:30`）；测试用协议注册与临时文件已清理还原。
- 至此可独立完成的验证全部收口：构建可复现、配置加载零错误、MediaInfo 端到端、本地化 en/zh 运行时切换、播放激活（CLI+协议）、打包 zip 标准格式与内容断言、启动冒烟。
- 剩余要求依赖用户输入/外部状态：三个决策（仓库结构 1.1、input_plus 1.3、menu.dll 1.4）、创建并推送目标 GitHub 仓库（当前 remote 为上游）、播放类 UI 实机授权（跨屏 HDR、右键动态菜单）。

### 9.19 input_plus.lua 移除（2026-08-07 第十五轮，用户决策 1.3）

- 用户确认不需要 input_plus.lua，已删除：`scripts/input_plus.lua` 移除；`input.conf` 删除 7 条绑定（F3/F4/F5、`,`/`.`、Ctrl+←/→）；`THIRD_PARTY_NOTICES.md` 删除对应行；`mpv-winui-lazy/README.md` 增加移除说明。
- 运行目录已同步（deploy /MIR 删除脚本、覆盖 input.conf；`input.conf.bak-20260807-menu` 为本地保留的 160 条菜单备份，其中含 input_plus 引用属预期）。
- 验证：仓库/运行目录/zip 均无 input_plus 残留（README 说明文字除外）；mpv 解析 `input.conf parsed: 79 binds`（86−7），无 [e]/[w]；zip 315 条目（原 316−1）、input.conf 内 0 条 `#menu:`。
- 依赖该脚本的快捷键功能（F3-F5 临时清滤镜/着色器、按住倍速、流媒体式跳转）随之不可用，README 已说明。

### 9.20 GitHub 发布完成（2026-08-07 第十六轮，用户授权“自己创建”）

- 创建公共仓库 **saillill/mpv-winui-player**（https://github.com/saillill/mpv-winui-player，默认分支 main）。
- 本地仓库原为浅克隆，首次推送 `index-pack failed`（缺对象），`git fetch --unshallow origin` 后推送成功；共 4 个本地提交（082aded、9d3a5ef、eded741、e681b83）与后续 CI 修复提交（1a225bd、169e7e5）。
- **GitHub Actions 实测**：首跑因 `if: secrets...` 在 step 级 if 非法（官方限制，解析期 422）失败；改为 job 级 env 桥接后通过。次跑因 C++ 侧 WebView2 build targets 与 WindowsAppSDK 传递依赖重复产出 `WebView2Loader.dll`（APPX1101）失败；修复为仅保留 winmd 引用、不导入 build targets（cppwinrt 投影仍需该 winmd，C++ 源码未用 WebView2 API），C++ 输出不再含重复 loader。三跑因 pwsh 7 把 robocopy 退出码当脚本退出码失败；`package.ps1` 末尾显式 `exit 0`。
- **最终 CI 全绿**（run 31188072975，5m41s）：Create Unpackaged ✓、Create Release Zip ✓、Upload ✓；MSIX/pfx 步骤按设计跳过（无 secrets）。
- **Release v1.0.16**：https://github.com/saillill/mpv-winui-player/releases/tag/v1.0.16 ，附件 `mpv-winui-win-x64-Release.zip`（CI 产物，315 条目、全正斜杠、含 MediaInfo.exe、input.conf 无 #menu:/input_plus），内容断言通过。
- 产物下载曾因网络极慢（~36KB/s）耗时约 26 分钟，最终校验为有效 zip。

### 9.21 功能扩展（2026-08-08 第一轮）

- **窗口标题改为 MPV**：`AppLang.AppName` 由 “mpv winui” 改为 “MPV”；UIA 实测 TitleBar/Text=MPV。
- **设置界面扩展（PotPlayer 式）**：`AppSettings` 新增 8 项（Hwdec/VolumeMax/KeepOpen/LoopFile/Deinterlace/AspectRatio/SubFontSize/SubDelay），新增 `MpvSettings` 映射到 mpv 命令；设置页新增 9 个选项（含此前未暴露的 EnableVideoPreview）；改动即时经 `AppContext.SendMpvCommand` 下发，启动时批量应用。mpv 日志实测 8 条 `Run command: set ...` 全部下发。
  - 修复：新选项 Integer Setter 收到 boxed int 后 `(double)v` 拆箱抛 InvalidCastException 导致设置窗口崩溃（0xC000027B），改用 `Convert.ToInt32`。
- **右键菜单恢复**：从本地备份还原 `input.conf`（153 条 `#menu:`，剔除已删的 input_plus 绑定），dyn_menu→menu-data→WinUI 渲染链路恢复；运行配置已部署。
- **更多语言**：新增 ja-JP / ko-KR / de-DE / fr-FR / es-ES / ru-RU（含设置标签共 113 键，8 个语言文件全部对齐、无空值）；设置页语言列表改为动态枚举 `Languages/*.json`。
- **验证**：Debug 构建 0 警告 0 错误；UIA 打开设置窗口正常并渲染全部新选项；默认英文菜单 File/View/Help、标题 MPV；启动冒烟正常退出。

### 9.22 功能扩展（2026-08-08 第二轮）

- **设置分类**：`Option` 增加 `Category`；`OptionListControl` 改为 `CollectionViewSource.IsSourceGrouped` 分组渲染（新增公开 `OptionGroup` + x:Bind 分组头，消除 AOT 警告）；UIA 实测设置窗口显示 General/Playback/Video/Audio/Subtitle/Paths 六个分组标题。
- **更多可保存配置与自定义路径**：新增 8 项——默认速度（speed）、字幕位置（sub-pos）、首选音频/字幕语言（alang/slang）、音频设备（audio-device）、截图目录（screenshot-directory）、截图文件名模板（screenshot-template）、缓存目录（cache-dir）；经 `MpvSettings` 即时下发、启动批量应用；mpv 日志实测 11 条 `set` 命令全部下发，空值语言/路径项正确跳过。
- **重启提示弹窗**：语言等需要重启的设置改动后弹出 ContentDialog（需要重启/立即重启/稍后），确认后保存窗口位置并 `AppInstance.Restart("Reset")`。
- **帮助/关于**：加入 mpv 官方手册（mpv.io/manual/master）与本项目（saillill/mpv-winui-player）链接，保留 mpv GitHub；UIA 实测链接全部渲染，对话框本地化（标题/关闭按钮走 AppLang）。
- **滤镜菜单扁平化**：`dynamic_menu.lua` 拆为 `vsr_menu`/`hdr_menu` 两个扁平子菜单（去掉“增强脚本”中间层），`清空所有脚本` 改为 input.conf 静态项；UIA 树实测 Nvidia VSR / RTX Video HDR / 清空所有脚本 同级直接显示。
- 语言文件新增 18 键（分类/新选项/重启弹窗），8 个语言文件均 131 键、无缺失无空值；Debug 构建 0 警告 0 错误。

### 9.23 功能扩展（2026-08-08 第三轮）

- **帮助菜单独立文档项**：Help 菜单新增“mpv Official Manual / mpv 官方手册”（Tag=mpv-docs，Launcher 打开 mpv.io/manual/master），本地化到 8 种语言；UIA 实测菜单项存在。
- **设置双栏排版**：SettingsPage 改为左侧分类 ListView + 右侧 OptionListControl（ShowHeaders=false 扁平列表），分类来自选项 Category 去重；修复了语言下拉初始化回填触发 Setter 导致误弹“需要重启”对话框的问题（值未变化不提示）。UIA 实测：左栏 General/Playback/Video/Audio/Subtitle/Paths，右栏对应选项，打开设置无弹窗。
- **右键菜单本地化**：应用启动后把界面语言写入 `user-data/mpvw/language`；`dyn_menu.lua` 内置 en-US 翻译表（约 120 条段名 + 动态轨道/章节等文案），解析时逐段本地化，并 observe 语言属性自动重解析；`dynamic_menu.lua` 同样本地化并 observe。实测：默认 menu-data 顶层为 Play/Pause/Stop/Navigation/Video/Audio/Subtitle/…/Filters && Enhance；切 zh-CN 后恢复中文（播放/暂停/导航/视频…）。
- 验证：Debug 构建 0 警告 0 错误；UIA（帮助项/设置双栏/无弹窗）+ mpv menu-data 双语实测通过。
