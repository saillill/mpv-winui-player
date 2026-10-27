# 部署与构建布局

`data/` 配置层如何部署到用户机器、构建产物在哪、哪些文件不能被覆盖。
数字可重算（`§6`）。**本章 §1 的路径分裂会直接影响部署结果**，见
[`mpv-conf.md`](mpv-conf.md) §1。

---

## 1. 两条部署路径（代码不一致）

| 触发者 | 源 | 目标 | 时机 |
|---|---|---|---|
| `ConfigDeployer.EnsureDeployed`（`AppContext.Init`） | `data/` | `%LOCALAPPDATA%\mpv-winui\mpv` | 启动，**同步** |
| `ConfigDeployer.EnsureDeployedAsync`（播放页） | `data/` | `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv` | 首次 `MpvPlayerPage.CreateAsync` |
| `data/deploy-config.ps1`（手动） | `data/` | `%LOCALAPPDATA%\mpv-winui\mpv` | 手工执行 |

第二行来自 `AppData.Current.OpenOrCreateLocalDataFolderAsync("mpv")`
（`MpvPlayerPage.xaml.cs:493`），而 `AppData.GetForUnpackaged("mpv-winui","mpv-winui")`
把 publisher 与 product 都拼进路径，于是目标多一层 `mpv-winui`。
**两层都会部署，但 mpv 只读后者的内容做 `config-dir`，而设置的写入点在前者。**
完整证据见 [`mpv-conf.md`](mpv-conf.md) §1.1。

`data/deploy-config.ps1` 自己写死单层目标（`:9` `Join-Path $env:LOCALAPPDATA 'mpv-winui\mpv'`），
与 `AppContext.Init` 一致、与 mpv 的实际读取不一致。

---

## 2. `data/` 的实际结构

共 **58** 个文件（`git ls-files data`）。

| 路径 | 文件数 | 说明 |
|---|---:|---|
| `data/` 根 | 5 | `mpv.conf`、`input.conf`、`profiles.conf`、`script-opts.conf`、`deploy-config.ps1` |
| `data/scripts/` | 18 | Lua 脚本 |
| `data/vs/` | 18 | VapourSynth `.vpy` |
| `data/script-opts/` | 8 | 各脚本的 `.conf` |
| `data/fonts/` | 3 | 两个字体 + OFL 许可 |
| `data/licenses/` | 3 | 本层许可 + 第三方汇总 + MediaInfo |
| `data/docs/` | 2 | 配置层自己的 README + 预览 API 说明 |
| `data/tools/` | 1 | `MediaInfo.exe` |

**为什么根目录只有这五个文件**：`data/docs/README.md` 记录的分层原则是
「mpv 强制要求放在配置根的留在根」——`mpv.conf` 与 `input.conf` 由 mpv 硬编码读取；
`profiles.conf` / `script-opts.conf` 被 `mpv.conf` 的 `include` 引用；
`deploy-config.ps1` 必须与部署目标同级才能用 `$PSScriptRoot` 定位源目录。

---

## 3. 部署规则

`ConfigDeployer.Sync`（`Platform/FileSystem/ConfigDeployer.cs`）的规则，
每条都有代价记录在源码注释里：

### 3.1 每次启动都同步，不是首次运行一次

层是 app 拥有的基础设施：早先的行为是「`mpv.conf` 存在就整体不做任何事」，
结果配置修复永远到不了已有安装——`[mpvw-hdr]` profile 永远停在首版。

### 3.2 用户拥有的文件永不被覆盖

`UserOwnedFiles`（`:44-49`）：
`mpv.conf`、`input.conf`、`menus.json`、`custom_menu.json`、
`saved-props.json`、`recent.json`、`deploy-config.ps1`。

`UserOwnedDirs`（`:55`）：`script-opts`——设置窗口会写这些文件，
镜像过去等于每次启动重置插件设置。缺失时仍会创建。

### 3.3 只有「我们部署且未被改过」的文件才刷新

用 `mpv-winui-deploy.manifest`（`<sha256>\t<relpath>`，纯文本）记录上次部署的哈希。
目标哈希与记录一致 → 我们的，可覆盖；不一致 → 用户改过，保留并记 warn。
没有 manifest 的旧安装：视为我们的，但**先备份**成 `<name>.bak-<timestamp>`。

**为什么 manifest 是纯文本而不是 JSON**：AOT 下 `System.Text.Json` 需要 source
generator；纯文本无依赖且便于人工排查同步问题（`ConfigDeployer.cs:388-390`）。

### 3.4 被删的文件也会被清掉

`PruneRemoved`（`:243-292`）：在旧 manifest 里、当前 bundle 不再提供、
且哈希仍是部署时的值 → 删除。少了这一步，把文件移进子目录会让旧副本永久留存
（一个 9 MB 的工具二进制是明显例子）。

### 3.5 排除的运行期目录

`ExcludedDirs = { cache, _cache, watch_later, gpu_cache, icc_cache }`（`:38`），
与 `data/deploy-config.ps1` 的 robocopy 排除表一致。这些目录不创建、不复制、不删除。

### 3.6 失败不阻断播放

部署失败只记日志（`EnsureDeployed` 与 `EnsureDeployedAsync` 都包了 try-catch），
应用继续用磁盘上现有的配置目录跑（mpv 退回默认值）。

---

## 4. 构建输出

### 4.1 构建（未打包）

```powershell
.\build.ps1 -Configuration Release -Platform x64
```

| 产物 | 路径 |
|---|---|
| 应用程序集 | `src\mpvw-app\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\mpvw.dll` |
| 原生组件 | `src\bin\x64\Release\mpv_winrt\mpv_winrt.dll`（`.winmd`、`.lib`、`.pdb` 同目录） |
| 测试 | `tests\conf\bin\Release\net10.0\`、`tests\native\bin\x64\Release\net10.0-windows10.0.26100.0\` |

程序集名是 **`mpvw`**（`<AssemblyName>`），不是 `mpv-winui`。`<TargetName>` 被显式设成
`$(AssemblyName)`，因为 Windows App SDK 的 XAML 目标用 `$(AssemblyName)` 生成中间程序集；
`resources.pri` 也被复制成 `mpvw.pri`。

**`Modules/` 目录不存在。** 应用代码按域分在 `src/mpvw-app/Domains/<域>/`，
非域层在 `Infrastructure/`、`Platform/`、`Lexicon/`、`Shell/`。
旧的 `mpv-winui/mpv-winui/Modules/` 布局在这次重构中已消失。

### 4.2 打包

| 脚本 | 产物 |
|---|---|
| `tools/package.ps1` | `dist/mpv-winui-win-<Platform>-<Configuration>.zip` |
| `tools/installer/build-installer.ps1` | `dist/mpv-winui-setup-*.msi`（WiX v5） |
| `tools/deploy-test.ps1` | 覆盖式部署到 `%MPVW_DEPLOY_DIR%`（默认同级 `mpv-deploy`） |

`package.ps1:34` 与 `build-installer.ps1:42` 都按 **`mpvw.exe`** 检查 publish 输出——
检查错名字会让脚本在完全正常的输出上报错（`package.ps1` 的注释记录了这次教训）。

### 4.3 原生库是分架构的

| 路径 | 内容 |
|---|---|
| `src/libs/include/` | 头文件，各架构共用 |
| `src/libs/lib/<Platform>/` | 导入库（`mpv.lib`、`mediainfo.lib`） |
| `src/libs/bin/<Platform>/` | 运行期 DLL（`mpv-2.dll`、`mediainfo.dll`） |
| `src/libs/share/` | mpv 数据文件 |

`libs/bin` 与 `libs/lib` **不入库**（`.gitignore` 的 `/src/libs`），由
`tools/fetch-mpv-libs.ps1` 下载并按 sha256 校验后解压。ARM64 没有上游产物，
脚本直说这一点而不是假装有。

---

## 5. 不能违反的三条

| 规则 | 原因（来自代码） |
|---|---|
| 不要让 `mpv.conf` 进入可刷新集合 | 它是用户可手改的，刷新会永久丢掉手改。见 `UserOwnedFiles` |
| 不要用 `File.WriteAllText` 写配置文件 | 先截断再填充，中断留下半个文件。用 `AtomicFile` |
| 不要在 `ConfigDeployer` 之后再动配置目录 | `AppContext.Init` 里部署是同步的，且必须早于 `ManagedMpvConfig` / `PluginConfigWriter` 的合并写——它们合并进**已存在**的文件 |

---

## 6. 重算本文每个数字

```powershell
git ls-files data | Measure-Object                                             # 58
git ls-files 'data/*' | ? { ($_ -split '/').Count -eq 2 }                      # 5 个根文件
foreach ($d in @('scripts','vs','script-opts','fonts','licenses','docs','tools')) {
  "{0,-14} {1}" -f $d, (git ls-files "data/$d/*" | Measure-Object).Count
}

Select-String -Path src\mpvw-app\Platform\FileSystem\ConfigDeployer.cs `
  -Pattern 'UserOwnedFiles|UserOwnedDirs|ExcludedDirs|ManifestName' -Context 0,8

Test-Path src\mpvw-app\Modules                    # False（目录已不存在）
Select-String -Path src\mpvw-app\mpv-winui.csproj -Pattern 'AssemblyName|TargetName'
Select-String -Path tools\package.ps1, tools\installer\build-installer.ps1 -Pattern 'mpvw\.exe'

# 本机实际部署到哪
Get-ChildItem $env:LOCALAPPDATA -Directory -Filter 'mpv-winui*' | % { "$_ -> " + (Get-ChildItem $_.FullName -Directory | % Name) }
```

---

## 7. 原因不明

- **两层都部署、只有一层被 mpv 读到**，是有意为之还是重构遗漏：`c36e1d9` 的提交说明
  只谈仓库目录，未提运行期路径（与 [`mpv-conf.md`](mpv-conf.md) §10 同一条）。
- **`data/deploy-config.ps1` 的排除表与 `ConfigDeployer` 的排除表为何要手工保持一致**：
  两处各写一份（`:38` 与 `deploy-config.ps1:12` 的 robocopy `/XD /XF`），
  注释说"mirrors"，但没有检查器验证它们真的相同。
