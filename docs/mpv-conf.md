# mpv.conf 的优先级模型

`mpv.conf` 是设置的唯一真源：设置界面是它的编辑器，不是另一份真相。
本文描述三档优先级、托管块的所有权判定、以及状态文件的作用。数字可重算（`§7`）。

---

## 1. 文件在哪（这里有一处代码冲突，先读）

| 角色 | 路径 | 代码 |
|---|---|---|
| 设置层读写的 `mpv.conf` | `%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf` | `ManagedMpvConfig.cs:120-124`；读走 `MpvSettings.cs:1192` |
| 部署层（`AppContext.Init`） | `%LOCALAPPDATA%\mpv-winui\mpv` | `AppContext.cs:108-111` |
| **mpv 实际读的目录** | **`%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv`** | `MpvPlayerPage.xaml.cs:493` → `AppData.GetForUnpackaged("mpv-winui","mpv-winui")`（`AppData.cs:22-24,52`）→ `MpvPlayer.cpp:250` 的 `config-dir` |

### 1.1 冲突（已修复）

**修复前**：设置层写到的目录，不是 mpv 读的目录。两边差一级 `mpv-winui`——设置层
`...\mpv-winui\mpv\`，mpv 读 `...\mpv-winui\mpv-winui\mpv\`。

实测证据（修复前）：`mpv-winui\mpv` 的 mpv.conf 719 行 / 有托管块 / 有状态文件；
`mpv-winui\mpv-winui\mpv` 的 mpv.conf 294 行 / 无托管块 / 无状态文件 /
`script-opts` 无托管标记。设置在界面里改对了也写进了文件，但那个文件 mpv 不读。

**成因**：`AppData.GetForUnpackaged(publisher, product)` 把两个参数都拼进路径
（两者都是 `"mpv-winui"`），产生双层目录；而 `ManagedMpvConfig`、`PluginConfigWriter`、
`MenuDefinitionSource`、`SettingsLayout`、`AppContext`、三个 `input.conf` 写入点
各自用 `Path.Combine(LOCALAPPDATA,"mpv-winui","mpv",…)` 硬拼单层。

**修法**：全部改走一个派生点 `AppData.Current.MpvConfigDirectory`
（`AppData.cs`），它建立在 `EnsureLocalFolder()` 已经解析出的根之上，
所以与 mpv 的 `config-dir` 由构造保证一致，不靠两份常量列表碰巧相同。

**已存在安装的数据迁移**：`ConfigDeployer.MigrateLegacyConfigDirectory` 在首次
`Sync` 时把旧目录折叠进来。策略按文件归属分：

| 文件 | 策略 | 理由 |
|---|---|---|
| `UserOwnedFiles`（mpv.conf、input.conf、menus.json…）与 `script-opts/` | **旧目录覆盖目标** | 旧副本才是设置窗口一直在写、用户一直在改的；目标里那份是打包原样。实测：旧 mpv.conf 719 行带两个标记块，目标 294 行什么都不带 |
| 其余层文件 | **目标保留** | 目标由同一 bundle 部署，至少一样新 |
| `ExcludedDirs`（cache 等） | 跳过 | 搬缓存等于复制几个 GB 再重建 |

目标文件在被覆盖前先保存为 `<name>.bak-premigration`。迁移完成后写
`mpv-winui-migrated.stamp`，之后不再重跑（否则每次启动都会用旧副本覆盖回去）。

### 1.2 另一个可见影响

`data/mpv.conf` 有 289 行，部署到双层目录后是 294 行。差 5 行是旧版本残留
（`gpu-context`、`title-bar`、`ytdl` 等已从源文件删除、部署副本里仍在），
因为 `mpv.conf` 在 `UserOwnedFiles` 里（`ConfigDeployer.cs:44-49`），一旦存在就永不刷新。
详见 [`deploy-layout.md`](deploy-layout.md)。

---

## 2. 三档优先级

```
高  ┌──────────────────────────────────────────────┐
    │ 手改的 mpv.conf：托管块之外的活动行            │  永不覆盖、永不下发
    ├──────────────────────────────────────────────┤
    │ 手改的 mpv.conf：托管块之内的行                │  原样保留，该项退出 wanted
    ├──────────────────────────────────────────────┤
    │ 界面设置 → 托管块                              │  常态写入区
    ├──────────────────────────────────────────────┤
    │ 界面设置 → 运行时 set                          │  最低，会话级
低  └──────────────────────────────────────────────┘
```

**「运行时 set 最低」是 mpv 的固有行为，不是本项目的设计选择。** mpv 先读配置
再接受命令，所以同一项同时出现在两者里时配置文件赢。这决定了规则：
**一个选项只能有一条写入路径。**

要让「手改最高」成立，靠的是**不发送**，不是写注释声明：
`MpvSettings.BuildApplyAllCommands()` 跳过 `MpvConfOverrides.IsUserOwned` 为真的项
（`MpvSettings.cs:419`）。同一个判定同时驱动界面上的「已被 mpv.conf 覆盖」标记和该行的
禁用状态，所以标记与实际行为是同一件事。

---

## 3. 两个标记块

| 块 | 起始标记 | 结束标记 | 谁写 |
|---|---|---|---|
| managed options | `# === mpv-winui managed options (do not edit) ===` | `# === end mpv-winui managed options ===` | 程序，每次设置改动重写 |
| user overrides | `# === mpv-winui user overrides (edit freely) ===` | `# === end mpv-winui user overrides ===` | 程序只在不存在时创建，之后不碰 |

标记常量在 `ManagedMpvConfig.cs:39-43`。

**user overrides 块必须在文件末尾**：mpv 由上往下读，写在后面的赢。于是
「手改最高」变成**文件内的位置关系**，不需要任何代码去仲裁。

块内的说明文字只在块中**只有注释和空行**时刷新（`ManagedMpvConfig.cs` 的
`FindUserOverridesBlock` → `IsUntouched`）。用户写进任何一行实质内容后，整块永不再碰。

---

## 4. 所有权：按行，不按块

`MpvConfOverrides` 把文件的每一行分成三种所有者（`MpvConfOverrides.cs:1-40`）：

| 所有者 | 判定 | 程序可否改 |
|---|---|---|
| 用户的（块外活动行） | 在托管块之外、且不等于打包模板里的任何一行 | 否 |
| 用户的（块内被改） | 行号在块内、且整行文本不在「上次我们写的行」记录里 | 否，原样携带 |
| 打包 body 的（未改动） | 在块外，且与 `data/mpv.conf` 的活动行逐字相同 | 是，但**不重复写进块** |
| 程序的（块内的行） | 整行文本命中「上次我们写的行」记录 | 是 |

### 4.1 为什么按整行而不是按选项名

`script-opts=a=b` 每一行都解析成选项名 `script-opts`。按名字判定会把程序自己写的
第二行起误判成手改。所以记录与比较都用**整行文本**（`ManagedMpvConfig.cs`
的 `ReadRecordedBlock` 文档）。

### 4.2 为什么需要「打包 body 的」这一档

文件有三段：打包 body、托管块、块之后的全部。把「块外即用户」当成规则，
会把打包 body 也算用户——它带着约一百条活动赋值，于是设置窗口几乎所有行都
被标成「已被 mpv.conf 覆盖」并禁用。症状看起来是「设置页什么都不显示」，
实际是全部只读。

判据是对比打包模板。没有模板时（源码检出直接运行）**一律当作用户的**——
安全方向是「不动别人的文件」，不是「凭猜测重写」。

### 4.3 `bodyOwned` 的作用

打包 body 已经原样携带的选项，**不写进托管块**。理由：块在 body 之后，
写进去的副本会赢，从而静默遮蔽 body 里那行有注释的、用户更可能去改的配置。

实测印证：`volume-max`、`input-ipc-server`、`icc-cache-dir`、`gpu-shader-cache-dir`
都在 `data/mpv.conf` 里有活动行（`:141`、`:45`、`:93`、`:96`），因此不在托管块中。

---

## 5. `mpv-winui-managed.json`

| 项 | 值 |
|---|---|
| 位置 | 与 `mpv.conf` **同目录**：`%LOCALAPPDATA%\mpv-winui\mpv\mpv-winui-managed.json` |
| 文件名常量 | `ManagedMpvConfig.cs:53` |
| 格式 | 每行 `<key>\t<hex(UTF-8)>`，两行 |
| 大小（本机实测） | 19751 字节 |

两个键：

| 键 | 内容 | 谁写 |
|---|---|---|
| `managed` | 上次写进托管块的行（含两个标记），hex 编码 | 每次写入后无条件写 |
| `body` | 部署时的 body 基线 | **只有旧版本写过**；现在优先读打包模板，这个键只为兼容老安装而先读 |

为什么 hex 编码：一个多行块要塞进一行。为什么用纯文本而非 JSON：AOT 下
`System.Text.Json` 需要 source generator，纯文本无依赖且便于人工查看
（与 `ConfigDeployer` 的 manifest 风格一致）。

### 5.1 只记录 `wanted`，不记录手改行

若把手改行也记进去，第二次启动会发现它「匹配记录」，于是把手改当成自己的、
重新可写——**手改只生效一次**。记录必须只描述程序写了什么。

### 5.2 为什么与 `mpv-winui-deploy.manifest` 分开

`ConfigDeployer` 的 manifest 记录的是**配置层文件**的哈希，且 `mpv.conf` 被
`UserOwnedFiles` 排除在外。混在一起会让「这个文件属于谁」无法推理。
两个文件、两套记录、两个用途。

### 5.3 状态文件写失败不阻塞

`ReadState` / `WriteState` 都包了 try-catch，失败只记日志。损坏的状态文件按
「不存在」处理——那会让所有块内行读成手改、程序停止覆盖，是安全方向。

---

## 6. 运行时下发的四个跳过集

`BuildApplyAllCommands()`（`MpvSettings.cs:394`）在发送前检查 5 个条件，
命中任一即跳过该项。前 4 个是集合，第 5 个是归属判定：

| 集合 | 行号 | 成员 | 含义 |
|---|---:|---|---|
| `MpvConfOverrides.IsUserOwned` | `:419` | 动态 | 手改赢，不发 |
| `ConfigOnlyKeys` | `:1055` | 8 个（见下） | 只能启动时生效，没有运行时命令 |
| `StartupDeferredKeys` | `:1087` | `HdrAutoMode` | 脚本还没注册，发了会静默失败 |
| `SessionStateKeys` | `:1108` | `Speed` | 重放会打断正在播放的会话 |
| `EmbeddingOwnedKeys` | `:1140` | `Mpv_idle`、`Mpv_force_window`、`Mpv_d3d11_output_mode` | 内嵌模式的 Initialize 已决定，重发会破坏它 |

`ConfigOnlyKeys` 的 8 个：`Mpv_player_operation_mode`、`Mpv_demuxer_rawvideo_size`、
`InputIpcServer`、`IccCacheDir`、`GpuShaderCacheDir`、`DemuxerCacheDir`、
`OverrideDisplayFps`、`VolumeMax`。

`EmbeddingOwnedKeys` 的注释记录了根因：启动批次 515 条命令，
`cut=152` 是 `no-osd set idle no`，它在 `Initialize` 设好 `idle=yes` 之后把 idle 关掉，
此后 `loadfile` 返回 0 但 mpv 什么都不做——「视频不能播放」的根因。

---

## 7. 写盘路径与原子性

```
设置改动 / 启动
  → ConfigWriteQueue.EnqueueCoalescing(ManagedMpvConfig.WriteAsync)
  → MpvConfOverrides.Refresh()   （读磁盘重新判定归属）
  → BuildConfigLines()           （696 个属性里能出线的那部分）
  → 逐行过滤 userOwned / bodyOwned
  → 拼新块 → 与磁盘上的旧块比较 → 变了才写
  → AtomicFile.WriteAllTextAsync（同目录临时文件 → flush → 改名）
  → WriteState("managed", …)     （无条件）
  → MpvConfOverrides.Refresh()
```

要点：

- **写前先 `Refresh()`**：关着应用时做的改动必须先被尊重。
- **`changed` 为假就不写盘**，避免每次启动都改 mtime。
- **`MpvConfWatcher.NoteOwnWrite()` 在写盘前戳计数器**，不是之后。
  否则 watcher 的重载起点可能落在旧值上，把程序自己写的东西误判成手改。
- **`AtomicFile`**：`File.WriteAllText` 先截断再填充，中途失败会留下半个文件。
  历史上有过 `mpv.conf` 因转义缺陷涨到 896 MB 的事故，隔离文件仍在
  `%LOCALAPPDATA%\mpv-winui\mpv\_corrupt-20260920-150845\`（939559174 字节）。

---

## 8. 已知限制

### 8.1 删除不被尊重

用户删掉托管块里的一行，下次启动会被重新写回。因为无法区分
「我要 mpv 的默认值」与「我在清理文件」。选择不尊重删除是为了不让界面失效：
若把删除也当成「该项归用户」，那一行会被禁用并永久显示覆盖标记，用户再也改不回来。
要改值请用界面，或用 user overrides 块。

### 8.2 退休行清理是硬编码的

`RetiredBlockPrefixes = { "script-opts=ytdl_hook-" }`（`ManagedMpvConfig.cs`）。
老版本写过、新版不再拥有的行会因记录不匹配而读成手改，永久留存。这个列表把它们
直接丢弃。它是显式清单，不是通用机制。

### 8.3 两个写入者仍在写 `hdr-reference-white` / `hdr-peak-percentile`

配置层（`data/profiles.conf` 的 `[HDR_generic]`）已移除这两个键，但仍是两个写入者：
`MpvPlayerPage_Display.cs:443-449`（显示代码）与设置行（`MpvSettings.Gen.cs:131-132`
经 `SettingsPage.Options.Gen.cs:47`）。落盘的托管块里能看到
`hdr-peak-percentile=0.0`、`hdr-reference-white=auto`。

---

## 9. 重算本文每个数字

```powershell
$la = $env:LOCALAPPDATA

# 两个目录的对照
foreach ($d in @("$la\mpv-winui\mpv", "$la\mpv-winui\mpv-winui\mpv")) {
    $f = "$d\mpv.conf"
    if (Test-Path $f) {
        $t = Get-Content $f
        "$d  lines=$($t.Count) active=$(($t | ? { $_ -match '^\s*[A-Za-z][A-Za-z0-9_-]*\s*=' }).Count) " +
        "managed=$(($t | Select-String 'managed options \(do not edit\)').Count) " +
        "user=$(($t | Select-String 'user overrides \(edit freely\)').Count)"
    }
}

# 托管块边界与内容（$b/$e 是 1-based；块内首行在下标 $b，末行在 $e-2）
$t = Get-Content "$la\mpv-winui\mpv\mpv.conf"
$b = ($t | Select-String 'mpv-winui managed options \(do not edit\)').LineNumber
$e = ($t | Select-String 'end mpv-winui managed options').LineNumber
"managed block $b..$e  inner $($e-$b-1)"                                   # 281..706, inner 424
$inner = $t[$b..($e-2)]
"assignments: " + (($inner | ? { $_ -match '^\s*[A-Za-z][A-Za-z0-9_-]*\s*=' }).Count)   # 424

Get-Content "$la\mpv-winui\mpv\mpv-winui-managed.json" | % { ($_ -split "`t")[0] }   # managed / body

# data/mpv.conf 与部署副本的差异（5 行残留）
Compare-Object (Get-Content data\mpv.conf) (Get-Content "$la\mpv-winui\mpv-winui\mpv\mpv.conf")

# bodyOwned 的证据（§4.3）：这些在 data/mpv.conf 里有活动行，故不进托管块
Select-String -Path data\mpv.conf -Pattern '^#?\s*(volume-max|input-ipc-server|icc-cache-dir|gpu-shader-cache-dir)\s*='

# 四个跳过集的成员
Select-String -Path src\mpvw-app\Domains\MpvConf\MpvSettings.cs -Pattern 'private static readonly HashSet<string> \w+Keys' -Context 0,14

# 两条路径定义：设置层硬拼单层，mpv 读 GetForUnpackaged 的双层
Select-String -Path src\mpvw-app\Domains\MpvConf\ManagedMpvConfig.cs -Pattern 'MpvConfPath' -Context 0,5
Select-String -Path src\mpvw-app\Domains\Playback\MpvPlayerPage.xaml.cs,`
  src\mpvw-app\Platform\FileSystem\AppData.cs -Pattern 'OpenOrCreateLocalDataFolderAsync|AppDataId|AppDataPublisher'
```

## 10. 原因不明

- **`MpvConfPath` 为什么不走 `AppData.Current.ResolveLocalData`**：`AppData.cs` 已提供该派生
  方法，四个写入点却各自硬拼路径。代码无注释解释。
- **`c36e1d9` 是否打算一并统一运行期路径**：那笔提交的说明只谈仓库目录，无法确认是遗漏
  还是刻意。
- **部署副本的 5 行残留为何不触发 `PruneRemoved`**：`mpv.conf` 在 `UserOwnedFiles` 里，
  prune 直接跳过它；这是设计，但"源文件删了行、副本仍在"的后果没有处理机制。
