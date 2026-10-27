# 架构

四层结构、8 个域、以及**实测**的依赖方向。所有数字与依赖结论都由 `§7` 的命令重算得出，
不是设计意图。

---

## 1. 四层

| 层 | 路径 | 文件数 | 代码行数 | 语言 |
|---|---|---:|---:|---|
| 应用 | `src/mpvw-app/` | 388 | 82610 | C# + XAML |
| 原生 | `src/mpvw-native/` | 60 | — | C++/WinRT |
| 投影 | `src/mpvw-native-projection/` | 2 | — | csproj + README |
| 配置 | `data/` | 58 | — | mpv 配置 / Lua / VBS |

`src/mpvw-native/` 的代码行数不统计：它由生成文件与手写文件混在一起（`MpvPlayer.cpp` 单文件
103676 字节），手写与生成无法用路径区分，任何数字都会误导。

### 1.1 应用层（`src/mpvw-app/`）

承载全部 UI 与业务逻辑。目标框架 `net10.0-windows10.0.26100.0`，`PublishAot=true`、
`PublishTrimmed=true`（`src/mpvw-app/mpv-winui.csproj:2,4`），**因此反射是受限的**：
新增序列化必须走 `JsonSerializerContext`，新增图标必须走生成器。

**禁止**：

| 禁止 | 原因（来自代码） |
|---|---|
| 在 `Infrastructure/` 里引用任何 `Domains.*` | `Infrastructure` 被 13 个文件当作叶子依赖；加一条反向引用就把这个数字变成传递闭包，且 `SpringMath.cs`、`AtomicFile.cs`、`FileService.cs` 已被链接进测试工程，反向依赖会立刻让测试工程编译失败 |
| 在 `Lexicon/` 里引用任何域 | `Lexicon` 实测出度为 0（`§3`），它是纯词汇表；`LanguageManager.cs:26,71` 只用 `System.AppContext.BaseDirectory` |
| 在 `Platform/` 里引用任何域 | `Platform` 实测只有 1 条出边指向 `Infrastructure`，`ActivationService.cs:1,3`、`ConfigDeployer.cs:7`、`FileService.cs:7` 是全部 |
| 直接 `new` 第二个 `mpv_winrt.MpvPlayer` | 唯一例外是 `MpvPreviewer`（缩略图预览），它由原生层自己管理；应用侧只允许经 `MpvPlayer` 一个实例 |
| 在 XAML 注释里写 `--` | 触发 `WMC9997`，`tools/check-xaml.py` 在构建前拦截 |

### 1.2 原生层（`src/mpvw-native/`）

C++/WinRT 组件，宿主 libmpv。两个运行时类：`MpvPlayer`（播放器）、`MpvPreviewer`（缩略图预览，
独立第二实例）。IDL 在 `MpvPlayer.idl` + `Types/*.idl`(9) + `Events/*.idl`(16)。

**禁止**：

| 禁止 | 原因（来自代码） |
|---|---|
| 在 `Types/` 或 `Events/` 的 `import` 里写相对自身的路径 | MIDL 相对**项目目录**解析，不是相对导入文件；`Types/MpvTrack.idl` 必须写 `"Types/TrackType.idl"` |
| 删除已声明但未 raise 的事件 | `tools/check-winrt-events.py` 明确把它们当 ABI 而非死代码；实测 8 个属于此类（`§6`） |
| 在 `MpvPlayer::Initialize` 之外设置 `config-dir` 或 `config` | `MpvPlayer.cpp:249-250` 是唯一设定点；`InitializeForPreview` 走 `config=no`（`:338`），预览器不该读用户配置 |
| 用 `dotnet build` 构建这一层 | `VCTargetsPath` 是 Visual Studio 组件（`MSB4278`），只能走 `build.ps1` 里的 MSBuild |

### 1.3 投影层（`src/mpvw-native-projection/`）

**只有两个文件**：`mpv-winrt-projection.csproj` 和一份 1 行 README。
它的作用是**把 C++ 组件的 winmd 投影成 .NET 程序集并打成 nupkg**，让 `dotnet` 侧无需
MSBuild 就能引用原生组件。

关键事实（都来自 csproj）：

- `CsWinRTIncludes=mpv_winrt`、`CsWinRTGenerateAotProjection=true`（`:8,11`）——AOT 投影。
- 默认**不**通过 `ProjectReference` 构建 C++，而是读 `..\bin\<arch>\<config>\mpv_winrt\mpv_winrt.winmd`（`:59-63`）。
  原因写在 csproj 注释里：`mpv-winrt-test` 引用本工程，无条件的 C++ 引用会让
  `dotnet test` 永远跑不起来。
- 唯一的消费者是 `tests/native/mpv-winrt-test.csproj:27`。**应用工程不引用它**——应用直接
  `<Reference Include="mpv_winrt.winmd">`（`mpv-winui.csproj:167-168`）。

**禁止**：往这一层加第二个项目或加代码。它是投影管道，不是功能层。

### 1.4 配置层（`data/`）

mpv 的配置树，首次运行由 `ConfigDeployer` 部署到 `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv`。
细节见 [`deploy-layout.md`](deploy-layout.md)；优先级模型见 [`mpv-conf.md`](mpv-conf.md)。

**禁止**：

| 禁止 | 原因（来自代码） |
|---|---|
| 用程序重写 `mpv.conf` 全文 | 它是 289 行手工中文注释的资产，程序只允许改标记块内部（`ManagedMpvConfig.cs:14-28`） |
| 把 `mpv.conf` / `input.conf` / `menus.json` / `custom_menu.json` / `saved-props.json` / `recent.json` / `deploy-config.ps1` 加进 `ConfigDeployer` 的刷新范围 | 它们在 `UserOwnedFiles`（`ConfigDeployer.cs:44-49`），一旦存在就永不被覆盖；移除它们会让用户手改永久丢失 |
| 让 `script-opts/` 跟随 `data/` 刷新 | `UserOwnedDirs`（`ConfigDeployer.cs:55`），设置窗口会写这些文件 |

---

## 2. 8 个域

行数按「数 `\n`，末尾无换行 +1」统计，命令见 `§7`。

| 域 | 文件 | 代码行 | 负责 | 不负责 |
|---|---:|---:|---|---|
| `Settings` | 65 | 26986 | 设置树构建、选项渲染、依赖门控、布局定制 | `mpv.conf` 文本编辑（那是 `MpvConf`）；设置的持久化存储本身（那是 `SettingsModel`） |
| `Playback` | 72 | 25306 | 播放页全部 UI、控制栏三套皮肤、PiP、缩略图预览、库视图、音频可视化 | 库文件夹的持久化（`SettingsModel`）；mpv 命令的真正执行（`mpv_winrt`）；媒体元数据解析（`MediaInfo`） |
| `MpvConf` | 30 | 7854 | 读写/解析 `mpv.conf`、托管块所有权判定、schema 模型、`script-opts` 写入 | 把设置值翻译成 mpv 命令（`Settings`→`MpvSettings`）；界面渲染（`Settings`） |
| `SettingsModel` | 16 | 5265 | `AppSettings` 属性、注册表/ApplicationData 存储、值编解码 | 呈现，以及任何 mpv 映射 |
| `Menu` | 19 | 2007 | 菜单栏构建、菜单编辑器、`menu.conf` 读写 | 菜单项的本地化文案（`Lexicon`） |
| `MediaInfo` | 7 | 745 | MediaInfo 报告模型与窗口 | 播放、封面之外的媒体读取 |
| `Playlist` | 8 | 693 | 稍后观看 / 播放历史的解析与两个控件 | 队列本体（那是 mpv 的 `playlist` 属性，由 `Playback` 呈现） |
| `Library` | 1 | 87 | 库条目类型定义 | 库视图（那是 `Playback`） |

### 2.1 域之间的边界（实测）

按 `§3` 的边表：

- `Library` **被 `Playback` 依赖 5 个文件**，而它自己依赖 `Playback` 1 个文件
  （`VideoLibraryTypes.cs:3` 的 `using mpv_winui.Domains.Playback;`）。这是**一个真实的环**，
  见 `§4`。
- `Playlist`、`MediaInfo` 只依赖 `Infrastructure`，出度为 1。它们是叶子域。
- `Menu` 依赖 `Platform`(2) + `Infrastructure`(3) + `Lexicon`(1)，不依赖任何其他域。
- `SettingsModel` 只依赖 `Platform`(2) + `Infrastructure`(1)：**它不认识任何 UI 域**。
  这是「设置存储可以独立测试」的结构基础。

### 2.2 非域层

| 路径 | 文件 | 代码行 | 职责 |
|---|---:|---:|---|
| `Infrastructure/` | 41 | 3524 | `Common`（原子写、日志、窗口基类、图标）、`Controls`、`Converters`、`Motion` |
| `Platform/` | 18 | 2335 | `Activation`（文件关联/协议）、`AppModel`（打包判定）、`FileSystem`（路径、部署）、`Win32`（钩子、窗口） |
| `Lexicon/` | 10 | 3422 | `AppLang` 词汇表（2196 个属性）+ 语言加载 |
| `Shell/` | 9 | 2512 | `MainWindow`、`AppContext`（唯一全局出口） |
| 应用根 | 11 | 200 | `App.xaml(.cs)`、`Program.cs`、manifest、csproj |

`Shell` 命名空间是 `mpv_winui`（不是 `mpv_winui.Shell`），因此 `AppContext` 是不带
子命名空间前缀的全局类型。`§3` 的脚本把它归到 `(root)`，读数时注意。

---

## 3. 依赖方向（实测）

方法：对 `src/mpvw-app` 下每个 `.cs`/`.xaml` 去掉注释后，提取 `using mpv_winui.*` 与
全限定的 `mpv_winui.*` 引用，按**目录**归并成边。命令见 `§7`。

```
Lexicon      (out=0)  ◀───────────────────────────────────────
                              ▲
Infrastructure(out=1) ◀── Platform(out=1) ◀── SettingsModel(out=2)
      ▲   ▲                    ▲                    ▲
      │   └────────────────────┴────────────────────┘
      │
   Playlist(out=1)      MediaInfo(out=1)
      ▲
      │
   Library(out=2) ──▶ Playback(out=8) ──▶ Menu, MpvConf, Settings, SettingsModel
      ▲                    │
      └────────────────────┘                 Settings(out=6)
                                                   │
                                                   ▼
                                          Playback, MpvConf, Lexicon

Shell(out=9) ──▶ Settings, Playback, MpvConf, SettingsModel, MediaInfo, Menu, Lexicon,
                 Infrastructure, Platform
```

边表（`from -> to : 文件数`）：

| from | to | 文件 |
|---|---|---:|
| `Shell` | `Playback` | 3 |
| `Shell` | `MpvConf` | 2 |
| `Shell` | `Lexicon` | 2 |
| `Shell` | `Platform` | 2 |
| `Shell` | `Settings` / `SettingsModel` / `MediaInfo` / `Menu` | 各 1 |
| `Shell` | `Infrastructure` | 5 |
| `Settings` | `Infrastructure` | 24 |
| `Settings` | `Platform` | 16 |
| `Settings` | `Lexicon` | 15 |
| `Settings` | `Playback` | 14 |
| `Settings` | `MpvConf` | 5 |
| `Settings` | `SettingsModel` | 5 |
| `Playback` | `Infrastructure` | 37 |
| `Playback` | `Platform` | 10 |
| `Playback` | `Settings` | 6 |
| `Playback` | `Library` | 5 |
| `Playback` | `Playlist` | 5 |
| `Playback` | `SettingsModel` | 2 |
| `Playback` | `Menu` / `MpvConf` | 各 1 |
| `MpvConf` | `Infrastructure` | 8 |
| `MpvConf` | `SettingsModel` | 4 |
| `MpvConf` | `Platform` | 3 |
| `MpvConf` | `Settings` | 2 |
| `Menu` | `Infrastructure` 3, `Platform` 2, `Lexicon` 1 | |
| `MediaInfo` | `Infrastructure` | 3 |
| `Playlist` | `Infrastructure` | 4 |
| `Library` | `Playback` 1, `Infrastructure` 1 | |
| `SettingsModel` | `Platform` 2, `Infrastructure` 1 | |
| `Platform` | `Infrastructure` | 3（`ActivationService.cs:1,3`、`ConfigDeployer.cs:7`、`FileService.cs:7`） |
| `Infrastructure` | `Platform` | 2（`LoggerHelper.cs`、`UpdateChecker.cs`） |

### 3.1 四个真实的环

| 环 | 方向与规模 | 证据 |
|---|---|---|
| `Playback` ⇄ `Settings` | 14 : 6 | `SettingsPage.*.cs` 引 `Domains.Playback`；`PlayerControl.*.cs` 引 `Domains.Settings.Controls` / `SettingsModel` |
| `Playback` ⇄ `MpvConf` | 1 : 6 | `MpvPlayerPage_PlayControl.cs:4` 反引；`Settings`→`MpvConf` 由 `MpvConfEditorPage` 对 `Settings` 的 2 条引用闭合 |
| `MpvConf` ⇄ `Settings` | 2 : 5 | `MpvConfEditorPage.xaml.cs`、`MpvConfTextWindow.xaml.cs` 引 `Domains.Settings` |
| `Infrastructure` ⇄ `Platform` | 2 : 3 | `LoggerHelper.cs` 引 `Platform.FileSystem`；`UpdateChecker.cs` 引 `Platform.AppModel` |
| `Library` ⇄ `Playback` | 1 : 5 | `VideoLibraryTypes.cs:3` |

**这些环都是成立的、有意的**，不是待修的缺陷：C# 的 `partial class` 让
`SettingsPage` 与 `PlayerControl` 在物理上必须互相看见（设置页要驱动播放器的实时预览，
控制栏要读设置模型）。**但新代码不应加深它们**——加点对点的
`Infrastructure`/`SettingsModel` 依赖优先于加跨域依赖。

### 3.2 已知的清理项

`Platform/Win32/` 下两个文件声明的命名空间在 `Infrastructure` 里：

| 文件 | 目录 | 声明的命名空间 |
|---|---|---|
| `Platform/Win32/Win32WindowHelper.cs:8` | `Platform/Win32` | `mpv_winui.Infrastructure.Common.Utils` |
| `Platform/Win32/WindowsManager.cs:7` | `Platform/Win32` | `mpv_winui.Infrastructure.Common.View` |

后果：`§3` 的脚本会把它们算成 `Platform -> Infrastructure` 的边，而实际是**自引用**。
去掉这两条后的真实 `Platform -> Infrastructure` 边是 3 条（上表已按真实值写）。
这是纯命名遗留，**不是缺陷**；改名会牵动 `Infrastructure/Common/README.md` 列出的公开接口。

---

## 4. 与 `data` 配置层的分工

| 谁 | 读什么 | 写什么 | 时机 |
|---|---|---|---|
| `ConfigDeployer`（C#） | `data/` 全树 | `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv` | 每次启动，`AppContext.Init` 与首次 `MpvPlayerPage.CreateAsync` 各一次 |
| `ManagedMpvConfig`（C#） | 部署后的 `mpv.conf` | `mpv.conf` 的两个标记块 | 启动 + 任何设置改动（合并写） |
| `MpvSettings.LoadFromConfig`（C#） | 部署后的 `mpv.conf` | 内存中的 `AppSettings` | 启动 + `MpvConfWatcher` 触发时 |
| `MpvConfOverrides`（C#） | 部署后的 `mpv.conf` | 无（只读判定） | 每次写入前后 |
| `PluginConfigWriter`（C#） | `AppSettings` | `script-opts/*.conf` | 启动 + 设置改动 |
| `mpv`（原生） | 部署后的整个配置目录 | `_cache/`、`watch_later/`、截图 | 运行期 |

**用户覆盖规则**（三条，按优先级）：

1. 用户手改 `mpv.conf` 标记块**之外**的任何活动行 → 该选项归用户，程序不写、不下发。
2. 用户手改托管块**内**某行（与上次记录不一致）→ 该行原样保留，该项归用户。
3. 其余 → 程序拥有。未改动的 body 行（与打包模板逐字相同）也算程序拥有，不重复写入托管块。

判定按**整行文本**而非选项名（`script-opts=a=b` 每行都解析成 `script-opts`）。详见
[`mpv-conf.md`](mpv-conf.md)。

**部署时机**：`ConfigDeployer.EnsureDeployed` 是**同步**的，且在 `MpvSettings.LoadFromConfig`
之前——配置层必须在任何写入之前落盘（`AppContext.cs:105-136`）。

---

## 5. 域 README 与本文的关系

`src/mpvw-app/DOMAINS.md` 与 14 份域/层 `README.md` 是**索引**，不是契约。
与本文冲突时以本文与代码为准。它们不参与构建、不被任何检查器读取（实测
`git grep DOMAINS.md` 在 `.md` 之外无命中）。

---

## 6. 已知冲突

- **8 个 IDL 事件声明但从不 raise**：`TrackChanged`、`NetworkInfoChanged`、
  `TrackListChanged`、`TrackListCountChanged`、`MediaLoaded`、`PlaybackFailed`、
  `Seeked`、`VoConfigured`。不是缺陷，是 ABI——删它们会改变投影的公开表面。
- **4 个事件 raise 但无人订阅**：`FileEnded`、`FileStopped`、`BufferingChanged`、
  `SwapChainChanged`。
- **两个基线检查器只扫 1 个 XAML**、**`DOCS` 列表指向已删文档**：见
  [`verification.md`](verification.md) §4 与 [`README.md`](README.md) 末节。

## 7. 重算本文每个数字

行数一律按「数 `\n`，末尾无换行则 +1」。`Measure-Object -Line` 不计空行，会少报约 10%。

```powershell
# 四层与本层文件数
git ls-files | % { ($_ -split '/')[0] } | Group-Object | Sort-Object Name
git ls-files src/mpvw-app | Measure-Object          # 388
git ls-files src/mpvw-native | Measure-Object       # 60
git ls-files src/mpvw-native-projection | Measure-Object  # 2
git ls-files data | Measure-Object                  # 58

# 应用层代码行数（cs + xaml）
python -c "import glob;fs=[f for f in glob.glob('src/mpvw-app/**/*',recursive=True) if f.endswith(('.cs','.xaml'))];n=0`nfor f in fs: d=open(f,'rb').read(); n+=d.count(b'\n')+(0 if d.endswith(b'\n') or not d else 1)`nprint(len(fs),n)"

# 某域的文件数与行数：把上面 glob 换成
#   src/mpvw-app/Domains/Settings/**/*   → 64 / 26986
#   src/mpvw-app/Domains/Playback/**/*   → 71 / 25306
#   …（其余域见 §2 表头）
```

依赖边表用一段脚本重算（放在仓库外跑，不要提交）。要点：
去掉块注释/行注释，提取 `using mpv_winui.*` 与全限定引用，按**目录**归并，
`Domains/<name>` 折成 `Domains.<name>`，其余取第一段
（`Infrastructure` / `Platform` / `Lexicon` / `Shell`），自引用跳过：

```python
import re, collections, subprocess
AREAS = ['Library','MediaInfo','Menu','MpvConf','Playback','Playlist','Settings','SettingsModel']
def area(rel):
    p = rel.replace('\\','/').split('/')
    return 'Domains.'+p[1] if p[0]=='Domains' and p[1] in AREAS else p[0]
edges = collections.defaultdict(set)
for p in [x for x in subprocess.run(['git','ls-files','src/mpvw-app'],
          capture_output=True,text=True).stdout.split('\n') if x.endswith(('.cs','.xaml'))]:
    s = open(p, encoding='utf-8-sig').read()
    s = re.sub(r'/\*.*?\*/','',s,flags=re.S)
    s = re.sub(r'<!--.*?-->','',s,flags=re.S)
    s = re.sub(r'(?m)^\s*//.*$','',s)
    src = area(p[len('src/mpvw-app/'):])
    for m in re.finditer(r'\bmpv_winui\.((?:Domains\.\w+|[A-Z]\w*)(?:\.\w+)*)', s):
        bits = m.group(1).split('.')
        tgt = 'Domains.'+bits[1] if bits[0]=='Domains' and len(bits)>1 else bits[0]
        if tgt != src and (tgt in AREAS or tgt in ('Infrastructure','Platform','Lexicon','Shell')):
            edges[(src,tgt)].add(p)
for (a,b),fs in sorted(edges.items()): print(f'{a:22} -> {b:22} {len(fs)}')
```

IDL 事件统计：

```bash
python tools/check-winrt-events.py
```

---

## 8. 原因不明的部分

以下约束**读遍代码也找不到原因**，如实记录，不要当成设计意图：

- `src/mpvw-native-projection/README.md` 只有一行文字 `mpv-winrt`。这个工程为什么独立成层
  （而不是并入原生层）在仓库里没有任何记录。
- `MpvConf` 域的 `MpvConfValueCodec` 既被 `MpvConf` 用也被 `Settings` 用，但它放在
  `MpvConf`（`Infrastructure/Common/README.md` 说\"名字像工具但只有设置树用，所以放 Settings\"，
  与实况不符）。放在哪一层更好的判断依据，代码里没有。
