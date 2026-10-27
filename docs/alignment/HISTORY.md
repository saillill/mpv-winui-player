# 历史检索索引（git archaeology only）

## 0. 这个文件是什么

**这是一份可检索的索引，不是评价。** 它只做三件事：把 733 个提交按功能域归拢、把主页/媒体库那一段逐提交列出来、给出能重新跑出每个数字的命令。

三条自我约束，贯穿全文：

1. **不判定。** 全文不出现「这修好了 X」「这个功能被删了应该恢复」「这是 bug」。每条只记录 diff 里机械可见的东西。diff 里看不出来的，单元格写 `未判定`。
2. **每个事实带锚点。** 要么是 commit sha，要么是 `path:line`。没有锚点的概括不写进这张表。
3. **不引用仓库 `docs/` 的任何结论。** 本文件只读过 `PROJECT_BASELINE.md` §1–§4 和 `TERMINOLOGY.md`，用途仅限「知道哪个目录属于哪个域」。仓库 `docs/` 的其余内容未被采信。

| 项 | 值 |
|---|---|
| 索引仓库 | `C:\Users\z2637\Documents\mpv-project\mpv-clean`（`https://github.com/saillill/mpv-winui-player.git` 的干净克隆） |
| 分支 | `main` |
| HEAD | `0f70f747fce6baf88459ce207b1d88edb6cc2dc7`（`0f70f74`，2026-10-26） |
| 提交总数 | **733** |
| 覆盖区间 | 全历史：首个提交 `8ad7118`（2026-07-15）→ 末个提交 `0f70f74`（2026-10-26），即 `8ad7118^..HEAD` |
| 日期跨度 | 2026-07-15 .. 2026-10-26（首尾相距 103 天，含首尾 104 个日历日） |
| 作者 | `saillill <2637298591@qq.com>` 626 个，`ikas <65895428+ikas-mc@users.noreply.github.com>` 107 个 |
| 本文件生成日期 | 2026-10-26 |
| 工具 | `git 2.55.0.windows.4`；Windows / PowerShell 7（`pwsh`） |

> 注：本索引在 **2026-10-26** 生成，而 HEAD 提交本身也是 2026-10-26。所以「末个提交日期」与「生成日期」同一天，这不是笔误。

### 0.1 生成本文件所用命令

全部命令都在仓库根目录执行。它们是本文所有数字的唯一来源；任何一条都能单独重跑。

```bash
# 基本盘
git rev-list --count HEAD                                  # 733
git log -1 --format='%H %ad' --date=short                  # 0f70f747... 2026-10-26
git log --reverse --format='%ad' --date=short | head -1    # 2026-07-15

# 作者分布
git log --format='%an <%ae>' | sort | uniq -c | sort -rn   # saillill 626 / ikas 107

# 主页-媒体库窗口的规模
git rev-list --count 0b17cc0..HEAD                         # 151
git log -1 --format='%H %ad %s' --date=iso 0b17cc0         # 2026-10-19 21:28:32 +0800

# 逐提交的文件清单（域归属的唯一依据）
git log --name-only -M --format='@@%H|%ad' --date=short
```

PowerShell 里同样的取数（本文 §2 的计数脚本就是这一段，可直接粘贴运行）：

```powershell
Set-Location C:\Users\z2637\Documents\mpv-project\mpv-clean
$log = git log --name-only -M --format='@@%H|%ad' --date=short
$cur = $null; $rows = @()
foreach ($l in $log) {
  if ($l -like '@@*') {
    if ($cur) { $rows += $cur }
    $p = $l.Substring(2).Split('|')
    $cur = [pscustomobject]@{ Sha = $p[0]; Date = $p[1]; Files = @() }
  } elseif ($l.Trim()) { $cur.Files += $l.Trim() }
}
if ($cur) { $rows += $cur }
$rows.Count      # 733
```

### 0.2 一个必须写在最前面的取数事实

`git log --name-only` 在遇到**重命名**时，**只输出新路径**，不输出旧路径，`--name-only` 也不支持 `--name-status` 的 `R100 old new` 形式。实测：`144650a`（2026-09-17，`reorganise: group the loose files in both projects by kind`）把 6 个文件从 `mpv-winui/mpv-winui/MainWindow*` 移动到 `mpv-winui/mpv-winui/Shell/MainWindow*`，`git show --name-status -M 144650a` 给出 6 条 `R100`，而 `git show --name-only -M 144650a` 只列出 6 条**新**路径，旧路径一条不出现。

后果：**按当前路径做域归属，会漏掉该文件在重命名之前的所有提交。** 本索引的处理方式是显式接受这一点，并在每个域里写明补的旧路径。所以：

- Shell 域补上了 `mpv-winui/mpv-winui/MainWindow[_.A-Za-z]*.(cs|xaml)` 这个正则（覆盖 `Shell/` 之前的旧位置），否则 `MainWindow.xaml` 的 7 个旧提交、`MainWindow.xaml.cs` 的 27 个旧提交会全部落在域外。
- 其余 5 个高流量文件的路径**从未变过**（`git log --name-status -M` 全历史里，`Styles/Styles.xaml`、`Modules/Player/MpvPlayerPage.xaml`、`MpvPlayerPage.xaml.cs`、`MpvPlayerPage_Library.cs` 没有任何 `R` 记录），所以它们不需要补。

```bash
# 复核这条事实
git show --name-status -M 144650a | Select-String '^R'   # 6 条 R100，MainWindow* -> Shell/MainWindow*
git show --name-only   -M 144650a | Select-String 'MainWindow'  # 只有新路径
```

---

## 1. 域索引（按功能域）

### 1.1 域的成员判定规则（这是唯一的判定口径）

**域归属只看每个提交改了哪些文件，不看提交标题。** 判定在两个层面同时进行，第二个层面是主口径：

- 主口径：`git log --name-only -M` 拿到的文件清单，对每个文件按下面的前缀/正则打标签，一个提交的域 = 它所有文件的标签并集。
- 副口径（仅用于交叉核对与加权）：`git show --numstat` 的增删行数，用来回答「这个提交在本域里有多大」。

八个域的成员谓词（原样照抄进脚本，可直接跑）：

```text
Shell    ^mpv-winui/mpv-winui/Shell/
         ^mpv-winui/mpv-winui/MainWindow[_.A-Za-z]*\.(cs|xaml)$
         ^mpv-winui/mpv-winui/(AppContext\.cs|App\.xaml|App\.xaml\.cs|Program\.cs)$
Player   ^mpv-winui/mpv-winui/Modules/Player/
Settings ^mpv-winui/mpv-winui/Modules/Settings/
MpvConf  ^mpv-winui/mpv-winui/(Modules/MpvConf/|MpvConfOptions/)
Menu     ^mpv-winui/mpv-winui/(Modules/Menu/|Menus/)
Language ^mpv-winui/mpv-winui/(Languages/|Modules/Language/)
Build    ^(tools/|\.github/|installer/)|^[^/]*\.(ps1)$|\.(csproj|vcxproj|slnx|props|targets|filters)$
Docs     ^docs/|^[^/]+\.md$
```

几个刻意的取舍，写在这里以免读者以为是遗漏：

- `Modules/Common/`、`Modules/FileSystem/`、`Modules/Activation/`、`Modules/About/`、`Modules/AppModel/`、`Modules/MediaInfo/`、`Modules/Motion/`、`Modules/Mpv/` **不单独成域**，也不并入任何域。理由是任务给的八域清单里没有它们，硬塞进 Player 会让 Player 的计数不可复核。它们贡献的提交落入 §1.4 的「未归域」。
- `mpv-winui/mpv-winrt/`（C++/WinRT 组件）、`mpv-winui/mpv-conf-test/`、`mpv-winui/mpv-winrt-test/`、`mpv-winui-lazy/`、`data/` 同样不归八域，理由同上。
- `MpvConfOptions/` 归 MpvConf（它是配置编辑器的选项 schema 数据）；`Menus/` 归 Menu（菜单数据）。
- `Languages/` 与 `Modules/Language/` 合为 Language，因为二者在同一个提交里几乎总是一起改。
- `Build` 域把 `*.csproj/*.vcxproj/*.slnx/*.props/*.targets/*.filters` 也算进来：工程文件的改动本身就是构建配置的改动。
- `Docs` 域的 `^[^/]+\.md$` 只匹配仓库**根目录**的 md（`README.md`、`README_zh-CN.md`、`AGENTS.md`），不匹配 `docs/` 下的（那是 `^docs/` 负责的）。

### 1.2 交叉计数怎么算的（以及为什么合计不等于 733）

**一个提交可以同时属于多个域。** 「加了 9 个语言键的 UI 提交」同时是 Player + Language + Build（`tools/gen-schema-options.py` 之类）+ Docs，把它只算一次会丢掉另外三个域，只算四次又会让合计大于 733。本索引两个数都给：

- **多标签计数**（§1.3 表里的「提交数」）：域 ∪ 文件的标签并集，一个提交在每个命中的域里各计一次。八个域合计 **1299**。
- **主域计数**（用于对账）：按 `Shell → Player → Settings → MpvConf → Menu → Language → Build → Docs` 的固定优先级，把每个提交只记在第一个命中的域里。八个域合计 **702**。

于是可以对账：

```
733 总数
- 31 未归域（见 §1.4）
= 702 已归域
= 702 主域计数合计         ← 一对一，可逐条复核
+ 597 因多域重复计入        (1299 - 702)
= 1299 多标签计数合计
```

`597` 这个数不是估的，拆开看：

```
702 已归域 = 358 单域提交 + 344 多域提交
1299 标签   = 358×1 + 344×2.74（多域提交平均 2.74 个标签）
597 重复    = 1299 − 702 = 344×1.74
```

（`358` / `344` / `2.74` 由 §1.2 脚本的同一份数据算得，复核见下面的 `$multi` 与 `$sum`。）

```powershell
# 复现 §1.3 的每一行（接 §0.1 的 $rows）
$rx = [ordered]@{
  Shell    = '^mpv-winui/mpv-winui/Shell/|^mpv-winui/mpv-winui/MainWindow[_.A-Za-z]*\.(cs|xaml)$|^mpv-winui/mpv-winui/(AppContext\.cs|App\.xaml|App\.xaml\.cs|Program\.cs)$'
  Player   = '^mpv-winui/mpv-winui/Modules/Player/'
  Settings = '^mpv-winui/mpv-winui/Modules/Settings/'
  MpvConf  = '^mpv-winui/mpv-winui/(Modules/MpvConf/|MpvConfOptions/)'
  Menu     = '^mpv-winui/mpv-winui/(Modules/Menu/|Menus/)'
  Language = '^mpv-winui/mpv-winui/(Languages/|Modules/Language/)'
  Build    = '^(tools/|\.github/|installer/)|^[^/]*\.(ps1)$|\.(csproj|vcxproj|slnx|props|targets|filters)$'
  Docs     = '^docs/|^[^/]+\.md$'
}
foreach ($k in $rx.Keys) {
  $m = $rows | Where-Object { ($_.Files | Where-Object { $_ -match $rx[$k] }).Count -gt 0 }
  '{0}`t{1}`t{2}`t{3}' -f $k, $m.Count, ($m.Date | Sort-Object)[0], ($m.Date | Sort-Object)[-1]
}

$multi = 0; $none = 0; $sum = 0
foreach ($r in $rows) {
  $d = @(); foreach ($k in $rx.Keys) { if (($r.Files | Where-Object { $_ -match $rx[$k] }).Count -gt 0) { $d += $k } }
  if ($d.Count -eq 0) { $none++ }
  if ($d.Count -gt 1) { $multi++ }
  $sum += $d.Count
}
"unclassified: $none"       # 31
"multi-domain: $multi"      # 344
"multi-label total: $sum"   # 1299
```

### 1.3 八个域的汇总表

「提交数」是多标签计数；「首/末提交」是该域内按提交日期排序的第一条与最后一条。

| 域 | 提交数 | 日期跨度 | 末次改动者 | 该域最热的文件（被改次数） |
|---|---|---|---|---|
| Shell | **84** | 2026-07-15 .. 2026-10-26 | `1b49ab9` saillill 2026-10-26 | `MainWindow.xaml.cs` (35) |
| Player | **322** | 2026-07-15 .. 2026-10-26 | `0d0e889` saillill 2026-10-26 | `Modules/Player/PlayerControl.xaml.cs` (118) |
| Settings | **305** | 2026-07-15 .. 2026-10-26 | `2f3247a` saillill 2026-10-26 | `Modules/Settings/SettingsPage.xaml.cs` (113) |
| MpvConf | **22** | 2026-08-18 .. 2026-10-25 | `9398df1` saillill 2026-10-25 | `Modules/MpvConf/Option/MpvConfOptionControl.xaml` (9) |
| Menu | **17** | 2026-08-11 .. 2026-10-25 | `9398df1` saillill 2026-10-25 | `Menus/menus.json` (10) |
| Language | **158** | 2026-07-15 .. 2026-10-23 | `3b662a8` saillill 2026-10-23 | `Languages/zh-CN.json` (143) |
| Build | **213** | 2026-07-15 .. 2026-10-26 | `0f70f74` saillill 2026-10-26 | `mpv-winui/mpv-winui/mpv-winui.csproj` (32) |
| Docs | **178** | 2026-07-27 .. 2026-10-26 | `0f70f74` saillill 2026-10-26 | `README.md` (63) |
| **合计（多标签）** | **1299** | | | |

每行的首条提交（域内最早）：

| 域 | 首条提交 | 首条提交的日期 | 该域第一次出现 |
|---|---|---|---|
| Shell | `3f4b3fa` add mpv-winui | 2026-07-15 | `Shell/` 目录本身到 `144650a`（2026-09-17）才存在，此前文件在 `mpv-winui/mpv-winui/` 根，靠补的 `MainWindow*` 正则覆盖 |
| Player | `3f4b3fa` add mpv-winui | 2026-07-15 | `Modules/Player/` 自首个 app 提交起就在 |
| Settings | `3f4b3fa` add mpv-winui | 2026-07-15 | 同批带入 `Modules/Settings/AppDataSetting.cs`、`AppSettings.cs` |
| MpvConf | `c58caee` add mpv conf editor | 2026-08-18 | 该域的最早提交即此条，可作起点 |
| Menu | `d195a4e` Drive menu bar from menus.json and unify UI localization | 2026-08-11 | 同上 |
| Language | `3f4b3fa` add mpv-winui | 2026-07-15 | `Modules/Language/AppLang.cs` 自首个 app 提交起就在；`Languages/*.json` 到 `082aded`（2026-08-07）才进 |
| Build | `8ad7118` add mpv-winrt | 2026-07-15 | 首个提交就是工程文件 |
| Docs | `599d2f4` add LICENSE and README | 2026-07-27 | 根目录 `README.md` 自此起 |

### 1.4 未归域的 31 个提交（显式列出，不藏在合计里）

这 31 个提交的文件全部落在八域之外，因此不参与 §1.3 的任何一行。

| sha | 日期 | 文件数 | 落点 |
|---|---|---|---|
| `30a091e` | 2026-07-15 | 1 | `.gitignore` |
| `cce292b` | 2026-07-18 | 2 | `mpv-winui/mpv-winrt/`（C++） |
| `a5a3ac1` | 2026-07-20 | 1 | `.gitignore` |
| `73bc1ba` | 2026-07-27 | 1 | `mpv-winui/mpv-winrt/.editorconfig` |
| `95c20bd` | 2026-07-27 | 1 | `mpv-winui/mpv-winui/.editorconfig` |
| `7da79cc` | 2026-07-27 | 2 | `mpv-winui/LICENSE.txt`、`mpv-winui/README.md` |
| `041fbd6` | 2026-07-27 | 1 | `screenshot/` |
| `9fe29fb` | 2026-07-28 | 0 | 合并提交（2 父），`--name-only` 无文件 |
| `eded741` | 2026-08-07 | 4 | `mpv-winui-lazy/` |
| `5d386ff` | 2026-08-12 | 2 | `mpv-winrt/` |
| `158935b` | 2026-08-12 | 1 | `Modules/Activation/` |
| `27058ed` | 2026-08-12 | 4 | `mpv-winui-lazy/` |
| `372e24e` | 2026-08-12 | 1 | `mpv-winui-lazy/deploy-config.ps1` |
| `617f3fd` | 2026-08-13 | 4 | `data/mpv/` |
| `27f3f97` | 2026-08-23 | 2 | `mpv-winrt/` |
| `dabcf47` | 2026-08-24 | 1 | `mpv-winui-lazy/input.conf` |
| `d99a9c6` | 2026-08-24 | 2 | `mpv-winui-lazy/scripts/` |
| `04e44cf` | 2026-08-24 | 1 | `Modules/Common/View/WindowStyleManager.cs` |
| `222be73` | 2026-08-30 | 2 | `mpv-winrt/` |
| `56a5964` | 2026-08-31 | 4 | `mpv-winrt/`、`mpv-winrt-test/` |
| `7fd7500` | 2026-08-31 | 2 | `data/mpv/` |
| `61d2d45` | 2026-09-11 | 2 | `mpv-winrt/` |
| `56feb65` | 2026-09-11 | 1 | `Modules/Activation/` |
| `cd0a315` | 2026-09-11 | 2 | `mpv-winrt/` |
| `cc98273` | 2026-09-13 | 0 | 合并提交（2 父），`--name-only` 无文件 |
| `5ba793b` | 2026-09-18 | 2 | `mpv-winui-lazy/scripts/` |
| `98d8e9c` | 2026-09-19 | 1 | `mpv-winui-lazy/mpv.conf` |
| `7d249e8` | 2026-10-19 | 3 | `mpv-winrt/` |
| `9eac404` | 2026-10-23 | 1 | `Modules/FileSystem/FilePickerHelper.cs` |
| `c5ffaf8` | 2026-07-31 | 0 | 合并提交（2 父） |
| `f664e84` | 2026-07-31 | 0 | 合并提交（2 父） |

四个 0 文件提交（`9fe29fb`、`c5ffaf8`、`f664e84`、`cc98273`）全部是 2 父合并提交：`git log --name-only` 对合并默认不出文件。它们**不是空提交**。复核：

```bash
git rev-list --parents -n1 9fe29fb c5ffaf8 f664e84 cc98273   # 每条 3 个字段 = 1 个 sha + 2 个父
git show --numstat --format='' cc98273                       # 有内容，例如 Modules/Player/... 
git show --name-status -m -M --format='' cc98273 | head      # 加 -m 才展开合并的 diff
```

> 如果改用「对合并加 `-m` 展开」的口径，`cc98273` 会落进 Player/Shell/Settings/… 多个域，八域合计随之上升。本索引**不用**该口径，因为 `-m` 会把同一个合并按父提交重复展开，计数不可复核。这是本索引的一个已知口径选择，不是数据缺失。

### 1.5 每个域的代表提交

每域 6–8 条，按「该域内改动行数最大」与「时间上分散」两条线各取若干。理由一律是一句机械描述，不评价。

#### Shell（84）— 窗口外壳 / 导航 / 标题栏

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `0b17cc0` | 2026-10-19 | Shell and player: home, two libraries, music page, queue popup, transport | 本域单次最大改动（本域 1716 行），`MainWindow.xaml` +497/−11、`MainWindow.xaml.cs` +768/−7 |
| `65860bd` | 2026-09-13 | Player: panel-true aspect lock, drag smoothness, SDR color, assoc repair | 第二大的 Shell 改动（461 行），**全部落在 `MainWindow_Aspect.cs`**（+419/−42），同批另动 `MainWindow_Size.cs` |
| `f580aee` | 2026-07-26 | add changing theme support | 主题切换进入外壳的起点，285 行集中在 `App.xaml`/`MainWindow.xaml` |
| `4bbb1fd` | 2026-07-15 | add unpackaged support | 本域最早提交，`AppContext.cs` 首次出现 |
| `98740dd` | 2026-08-12 | Split config-write queue and language-file loading out of AppContext | 从 `AppContext.cs` 拆出职责，`AppContext.cs` 的 20 次改动里的一次结构性拆分 |
| `05befbf` | 2026-10-20 | Shell: the open-file button gets one owner, and the owner knows the section | 引入 `ActivePlayer_LibrarySectionChanged`、`UpdateShellOpenFileButton` 与字段 `_activeSection` |
| `144650a` | 2026-09-17 | reorganise: group the loose files in both projects by kind | `MainWindow*` 从项目根移入 `Shell/`（6 条 `R100`），唯一一次目录重排 |
| `1b49ab9` | 2026-10-26 | ui: put 首页 打开文件 on the heading row, where every other section's action is | 本域末次改动，`MainWindow.xaml.cs` +17/−6 |

#### Player（322）— 播放页 / 播放控制 / 媒体库 / 队列 / 预览 / PiP / 全屏 / 音频

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `0b17cc0` | 2026-10-19 | Shell and player: home, two libraries, music page, queue popup, transport | 本域单次最大改动（6787 行），新增 `MpvPlayerPage_Library.cs`（+1898）、`MpvPlayerPage_Music.cs`（+311）、`MpvPlayerPage_PlaylistPanel.cs`（+332）、`LyricsLoader.cs`（+361） |
| `3682f0e` | 2026-09-13 | merge: namespace normalization for fork PlayerControl partials | 第二（4955 行），`PlayerControl` 各分部统一命名空间 |
| `72dca0f` | 2026-09-02 | refactor player control and add more sytle | 第三（4379 行），`PlayerControl` 拆分重构 |
| `3f4b3fa` | 2026-07-15 | add mpv-winui | 本域最早提交，`MpvPlayerPage*.cs` 8 个分部、`PlayerControl.xaml*` 同批进入 |
| `7cab636` | 2026-10-20 | Video library: 16:9 landscape cards, sized by the column they land in | 新增 `Modules/Common/View/AspectHeightConverter.cs`，XAML +29/−11 |
| `700bdea` | 2026-09-13 | menu bar: restore the config-driven builder wiring lost to the merge | `MpvPlayerPage_MenuBar.cs`（该文件 42 次改动）的一次接线修复 |
| `74ad169` | 2026-10-26 | logging: make the four measurement blind spots stop lying | 新增 `PlayChain.cs`，`MpvPlayerPage.xaml.cs` +53 |
| `0d0e889` | 2026-10-26 | ui: returning to a playing track restores the music page | 本域末次改动，`MpvPlayerPage_Library.cs` +12 |

#### Settings（305）

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `a5bad38` | 2026-09-19 | settings: put the 549 schema options on the page, generated | 本域单次最大（本域 12310 行），`SettingsPage.Options.Gen.cs` 类文件的大规模生成 |
| `9aa3a11` | 2026-09-20 | settings: split the generated General bucket into themes | 第二（11438 行），拆 `General` 桶 |
| `044ac9c` | 2026-08-10 | Split SettingsPage into partial files by responsibility | 第三（10036 行），`SettingsPage` 分部化，是 `SettingsPage.xaml.cs` 达 112 次改动的前提 |
| `d9ae7ef` | 2026-08-29 | Settings: section drill-down with breadcrumb (Windows-style IA) | 引入分节下钻 + 面包屑的交互形态 |
| `e1f40dd` | 2026-09-18 | ui: give the option card one shell instead of eleven copies | 选项卡片外壳从 11 份收敛为 1 份 |
| `2f3247a` | 2026-10-26 | settings: the startup batch set idle=no, which stopped every video from playing | 本域末次改动 |
| `4bbb1fd` | 2026-07-15 | add unpackaged support | 本域最早提交 |

#### MpvConf（22）— 配置编辑器

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `1f536f0` | 2026-09-18 | settings: generate the mpv option schema instead of leaving the editor | 本域单次最大（本域 13548 行），选项 schema 进入生成流程 |
| `d9fc05a` | 2026-10-19 | Settings: rebuilt shell, manual-sourced descriptions, mpv.conf editor | 第二（5447 行），`mpv.conf` 编辑器同批改写 |
| `c58caee` | 2026-08-18 | add mpv conf editor | 第三（2530 行）且是本域最早提交，`Modules/MpvConf/` 自此存在 |
| `038908f` | 2026-08-21 | refactor mpv conf editor | 早期一次整体重构 |
| `6c6a10c` | 2026-08-22 | add support for editing profiles and refactor schema json | profile 编辑 + schema json 结构变更 |
| `9398df1` | 2026-10-25 | typography: tokenise 76 spacing and font-size literals, prove the pixels | 本域末次改动，`MpvConfOptionControl.xaml`（该域 8 次改动的最热文件）在内 |

#### Menu（17）— 右键菜单 / 菜单编辑器

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `9bbd7f6` | 2026-09-02 | using mpv menu conf format for the menu bar add a menu editor | 本域单次最大（1602 行），菜单格式切换 + 菜单编辑器同批 |
| `a944749` | 2026-08-14 | Audit and rebuild settings UX, add missing mpv options and perf fixes | 第二（281 行），`Menus/menus.json` 在内 |
| `a304540` | 2026-08-12 | Add Display info dialog under Help; close I1/I2 as mpv-side limits | 第三（246 行），`Menus/menus.json` 菜单项增补 |
| `d195a4e` | 2026-08-11 | Drive menu bar from menus.json and unify UI localization | 本域最早提交，菜单栏开始由 `menus.json` 驱动 |
| `69161e7` | 2026-08-12 | Add standalone menu editor for the user menus.json override | 独立菜单编辑器窗口首次出现 |
| `9398df1` | 2026-10-25 | typography: tokenise 76 spacing and font-size literals, prove the pixels | 本域末次改动 |

#### Language（158）— 多语言

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `38565f7` | 2026-09-14 | settings: fix customize-mode rebuild clobber, tree-scoped add target | 本域单次最大（本域 20946 行），9 个 `Languages/*.json` 同时大改 |
| `a5bad38` | 2026-09-19 | settings: put the 549 schema options on the page, generated | 第二（3642 行），生成的行需要 9 种语言的键 |
| `d9fc05a` | 2026-10-19 | Settings: rebuilt shell, manual-sourced descriptions, mpv.conf editor | 第三（3271 行） |
| `a3d20d4` | 2026-08-24 | Language: add Traditional Chinese (zh-TW); drop stray Custom entry on language option | `Languages/zh-TW.json` 首次进入，语言集到 9 个 |
| `082aded` | 2026-08-07 | Prepare GitHub release: reproducible build/package, localization, config cleanup | `Languages/*.json` 目录自身首次出现（`Modules/Language/AppLang.cs` 更早，在 `3f4b3fa`） |
| `3b662a8` | 2026-10-23 | settings, pickers, bindings: the three migrations the review called for | 本域末次改动 |

#### Build（213）— 构建 / 部署 / 自检脚本

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `7a3bc6d` | 2026-10-22 | settings: the startup batch was never applied, and its commands were malformed | 本域单次最大（本域 1776 行），一次性新增 `tools/mvpw-mcp/` 下 13 个诊断脚本 |
| `d01cd95` | 2026-10-22 | audit: measure each domain in a running app, and fix what the measuring found | 第二（1066 行） |
| `fb130ca` | 2026-10-24 | docs+tools: a repair task that can run unattended for many rounds | 第三（965 行），`tools/ui-repair/` 驱动脚本 |
| `9f33719` | 2026-08-24 | Upgrade WindowsAppSDK group: Foundation 2.3.9, Runtime 2.4.0, WinUI 2.3. | `*.csproj` 的 WindowsAppSDK 版本组更新 |
| `8ad7118` | 2026-07-15 | add mpv-winrt | 本域最早提交，`mpv-winrt.vcxproj` 等工程文件 |
| `0f70f74` | 2026-10-26 | docs: alignment baseline, measured on 0d0e889 | 本域末次改动（同一条也是 Docs 域末次改动） |

#### Docs（178）

| sha | 日期 | 标题 | 为什么代表 |
|---|---|---|---|
| `2a1c100` | 2026-10-19 | Docs: deploy layout, settings contract, two handoffs and the motion system | 本域单次最大（1231 行） |
| `895b0cd` | 2026-09-20 | docs: correct the documents that were pointing at the wrong thing | 第二（1196 行） |
| `5050152` | 2026-10-22 | audit+skin: two inventories, and one prediction of theirs I could test | 第三（1120 行） |
| `599d2f4` | 2026-07-27 | add LICENSE and README | 本域最早提交，根 `README.md` 自此存在 |
| `6dddd9d` | 2026-09-18 | docs: trim the README to the product overview | `README.md`（该域 63 次改动的最热文件）的一次删减 |
| `049efb3` | 2026-10-25 | QUEUE: the menu editor is measured; history is structured but unreachable | `docs/ui-repair/QUEUE.md`（21 次改动，`--name-only` 口径）的一次状态更新 |
| `0f70f74` | 2026-10-26 | docs: alignment baseline, measured on 0d0e889 | 本域末次改动，新增 `docs/alignment/` 三份文件 |

---

## 2. 主页 / 媒体库时间线

### 2.1 窗口定义与口径

- 起点：**`0b17cc0`**，`2026-10-19 21:28:32 +0800`，标题 `Shell and player: home, two libraries, music page, queue popup, transport`。该提交动 44 个文件、+7953/−2677（合计 10630 行），其中**新增 5 个文件**：`MpvPlayerPage_Library.cs`(+1898)、`MpvPlayerPage_Music.cs`(+311)、`MpvPlayerPage_PlaylistPanel.cs`(+332)、`LyricsLoader.cs`(+361)、`LibraryEntry.cs`(+194)；其余为对既有 `MpvPlayerPage.xaml`(+1378/−203)、`MainWindow.xaml`(+497/−11)、`MainWindow.xaml.cs`(+768/−7)、`Styles/Styles.xaml`(+422/−31) 的改写。
- 窗口：`0b17cc0..HEAD`，即严格**之后**（不含 `0b17cc0` 本身）。
- 窗口规模：**151 个提交**（`git rev-list --count 0b17cc0..HEAD` = 151；`--no-merges` 同样是 151，说明窗口内无合并提交）。窗口内只有一个作者：`saillill`，151/151。
- 窗口日期跨度：2026-10-19 .. 2026-10-26。

```bash
git rev-list --count 0b17cc0..HEAD                 # 151
git rev-list --count --no-merges 0b17cc0..HEAD     # 151
git log --format='%an' 0b17cc0..HEAD | sort -u     # saillill
```

### 2.2 「触及主页或媒体库」的判定：两层，都写死

任务要求「主页或媒体库」，而这两者在代码里是**同一批文件**：主页 `home` 是 `MpvPlayerPage` 的一个 section（`MpvPlayerPage_Library.cs:57` `internal const string LibrarySectionHome = "home";`），与 `music`(:59)、`videos`(:61)、`queue`(:68)、`playlists`(:70) 并列，共享 `LibraryHost`(:1085 XAML)。所以判定用两层，都给出精确命令：

**A 层 — 专属文件（文件存在即证明与主页/库相关）。** 10 个文件：

```text
mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs
mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Music.cs
mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_PlaylistPanel.cs
mpv-winui/mpv-winui/Modules/Player/LibraryEntry.cs
mpv-winui/mpv-winui/Modules/Player/LibraryEntry.Selection.cs
mpv-winui/mpv-winui/Modules/Player/LibraryIndexLetter.cs
mpv-winui/mpv-winui/Modules/Player/LibraryTagsReader.cs
mpv-winui/mpv-winui/Modules/Player/MusicTags.cs
mpv-winui/mpv-winui/Modules/Player/PinyinInitial.cs
mpv-winui/mpv-winui/Modules/Player/IndexRailHover.cs
```

```bash
git log --format='%h' 0b17cc0..HEAD -- \
  mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs \
  mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Music.cs \
  mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_PlaylistPanel.cs \
  mpv-winui/mpv-winui/Modules/Player/LibraryEntry.cs \
  mpv-winui/mpv-winui/Modules/Player/LibraryEntry.Selection.cs \
  mpv-winui/mpv-winui/Modules/Player/LibraryIndexLetter.cs \
  mpv-winui/mpv-winui/Modules/Player/LibraryTagsReader.cs \
  mpv-winui/mpv-winui/Modules/Player/MusicTags.cs \
  mpv-winui/mpv-winui/Modules/Player/PinyinInitial.cs \
  mpv-winui/mpv-winui/Modules/Player/IndexRailHover.cs | wc -l
# 26
```

**B 层 — 共享文件上的库标识符改动。** 这 5 个文件同时承载播放器与媒体库，仅凭「改了它」无法归因，所以只在 diff 命中库标识符时计入：

```powershell
git log --format='%h' 0b17cc0..HEAD `
  -G'Library[A-Z]|Music[A-Z]|Album[A-Z]|Song[A-Z]|Artist[A-Z]|Folder[A-Z]|Queue[A-Z]' `
  -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml `
     mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml.cs `
     mpv-winui/mpv-winui/Styles/Styles.xaml `
     mpv-winui/mpv-winui/Shell/MainWindow.xaml `
     mpv-winui/mpv-winui/Shell/MainWindow.xaml.cs
# 17
```

`-G` 而不是 `-S`：`-G` 匹配「改动行里出现该模式」，`-S` 匹配「该字符串出现次数发生变化」。前者在这种「加一行使用既有类型」的场景里更稳；实测同一窗口 `-S 'LibrarySectionHome'` 只返回 5 条（全历史），而 `-G` 返回 17 条。两条命令都列在 §4.3 里。

**合计与去重：**

```
A 层 26 + B 层 17 = 43
两层重叠 13
并集 = 30
```

```powershell
$a = git log --format='%h' 0b17cc0..HEAD -- <§2.2 A 层 10 个文件>
$b = git log --format='%h' 0b17cc0..HEAD -G'Library[A-Z]|...' -- <§2.2 B 层 5 个文件>
"overlap: $((Compare-Object $a $b -IncludeEqual -ExcludeDifferent).Count)"   # 13
"A only:  $((Compare-Object $a $b | ? SideIndicator -eq '<=').Count)"         # 13
"B only:  $((Compare-Object $a $b | ? SideIndicator -eq '=>').Count)"         # 4
"union:   $((($a + $b) | Sort-Object -Unique).Count)"                         # 30
```

> **结论：151 个提交中，30 个触及主页/媒体库，121 个没有。** 这三个数可互相校验：30 + 121 = 151。

### 2.3 只被 B 层捕获的 4 个提交（说明 B 层不是冗余）

`05befbf`、`071d5d9`、`466f11e`、`7cab636` —— 它们改的共享文件里出现库标识符，但没碰任何 A 层专属文件。其中 `7cab636` 只改 `MpvPlayerPage.xaml`（+29/−11）与新增的 `AspectHeightConverter.cs`，A 层完全看不到它，而它确实改了媒体库卡片的尺寸规则。这就是必须留 B 层的原因。

同时，B 层会带进两个**非**主页/库的提交，本索引保留它们并在此标注，不静默剔除：

- `071d5d9`（high contrast）：命中原因是 `Styles.xaml` 新增 `x:Key="MpvMusicPageBrush"`、`x:Key="MpvMusicArtworkFallbackBrush"` 两个高对比度画刷 —— 是配色字典，不是库逻辑。
- `466f11e`（player: subscribe FileLoaded）：命中原因是新增行 `_mediaPlayer.FileStarted += MpvPlayerPage_LibraryFileStarted;` —— 事件订阅，处理器定义在 `MpvPlayerPage_Library.cs:2973`，但改动本身在播放链上。

保留而非剔除，是因为剔除要引入「我认为它算不算」的判断，那是评价不是索引。判定标准保持在「文件 + 标识符」这一层。

### 2.4 逐提交表（按时间正序，30 行）

「机械变化」一列只写 diff 里看得见的东西；从 diff 归纳不出的写 `未判定`。

| # | sha | 日期 | 作者 | 标题 | 触及的主页/库文件 | 机械变化 |
|---|---|---|---|---|---|---|
| 1 | `3017c3c` | 2026-10-19 | saillill | Music: real cover art and tags, exported from the native layer | `MpvPlayerPage_Music.cs` | `MpvPlayerPage_Music.cs` +119/−28：新增 `ReadMediaTags()`、`TagOr()`，`UpdateMusicLines` 从按 `" - "` 切 `media-title` 改为先读标签（`title`/`artist`/`album`），无标签才回落 |
| 2 | `37a649c` | 2026-10-19 | saillill | Music library: the Albums and Artists tabs actually group, and an album page | `LibraryEntry.cs`(+85)、`LibraryTagsReader.cs`(+82)、`MusicTags.cs`(+24)、`MpvPlayerPage.xaml`(+273)、`MpvPlayerPage_Library.cs`(+444/−14)、`AppLang.Shell.cs`(+45)、`en-US.json`/`zh-CN.json`(+19/−1) | 新增 20 个 `x:Name`（`AlbumHost`、`AlbumTrackList`、`AlbumSummary`、`AlbumColumnTitle/Artist/Genre/Length`…）与 18 个方法（`OpenAlbum`、`CloseAlbum`、`PlayAlbum`、`BuildMusicTagGroups`、`ApplyAlbumChrome`…）；新增 8 个 `lang.LibraryAlbum*`/`lang.LibraryColumn*` 键 |
| 3 | `1066ffe` | 2026-10-20 | saillill | Music: a real spectrum, the cover's colour, and a reading frame for the lyrics | `Audio/AlbumColorExtractor.cs`(+241)、`Audio/ArtworkPalette.cs`(+94)、`Audio/AudioCapture.cs`(+235)、`Audio/SpectrumAnalyzer.cs`(+259)、`LyricsLoader.cs`(+28)、`MpvPlayerPage.xaml`(+138/−77)、`MpvPlayerPage_Music.cs`(+254/−48) | 新增 `Modules/Player/Audio/` 四个文件与 3 个测试文件（+514）；新增 `x:Name="MusicPageWash"`；新增方法 `Analyze`、`Fft`、`Extract`、`ApplyPalette`、`ScrollLyricsTo`、`OnRhythmRendering` 等 |
| 4 | `19cfeb5` | 2026-10-20 | saillill | Shell: the disabled transport stayed grey for one frame, and the album page covered the library | `MpvPlayerPage_Library.cs`(+36/−10)、`PlayerControl.Animation.cs`(+7/−5)、`PlayerControl.PanelAnimation.cs`(+2/−2)、`PlayerControl.xaml.cs`(+21/−1) | 新增 `HideSurfacesOverLibrary()`，把原先散在调用点的 `_openAlbum = null; _albumTracks = []; AlbumHost.Visibility = Collapsed; MotionBehaviors.Reset(AlbumHost)` 收进一个方法；新增 `ApplyTransportRestOpacity()` |
| 5 | `8cb9f27` | 2026-10-20 | saillill | Music: the cover was lost because the tag pass replaced every card and dropped its thumbnail | `MpvPlayerPage_Library.cs`(+40/−2) | 打标签的循环里，`_libraryAll[i] = LibraryEntry.FromFileWithTags(...) ?? entry` 改为先取 `replaced`，`null` 时 `continue`，否则 `replaced.SetThumbnail(existing)` 再赋值；新增 `internal event Action<string>? LibrarySectionChanged` 与 `internal string CurrentLibrarySection`；`_librarySection = key` 后调用 `LibrarySectionChanged?.Invoke(...)` |
| 6 | `05befbf` | 2026-10-20 | saillill | Shell: the open-file button gets one owner, and the owner knows the section | `Shell/MainWindow.xaml.cs`(+43/−8) | 新增字段 `_homeEmpty`、`_activeSection`，新增 `ActivePlayer_LibrarySectionChanged(string)`、`UpdateShellOpenFileButton()`；订阅 `page.LibrarySectionChanged` |
| 7 | `7cab636` | 2026-10-20 | saillill | Video library: 16:9 landscape cards, sized by the column they land in | `MpvPlayerPage.xaml`(+29/−11)、`Modules/Common/View/AspectHeightConverter.cs`(+48) | 新增 `AspectHeightConverter`（含 `Convert`/`ConvertBack`）；XAML 引入 `xmlns:view="using:mpv_winui.Modules.Common.View"`；`StaticResource` 由 `MpvSpaceS` 换为 `MpvSpaceM` 与 `SixteenByNine` |
| 8 | `b485adf` | 2026-10-20 | saillill | Video folders: the header shows a glyph, the folder's name, and its path | `MpvPlayerPage.xaml`(+48/−4)、`MpvPlayerPage_Library.cs`(+55/−16) | 新增 `CardLayout` 方法；XAML 新增文件夹标题的图标 + 名称 + 完整路径三列；新增 `StaticResource CaptionTextBlockStyle` |
| 9 | `dc63a7d` | 2026-10-20 | saillill | LibraryEntry: the two album-card captions, as data with their fallbacks | `LibraryEntry.cs`(+24) | 新增两个 `init` 属性 `AlbumTitle`、`AlbumArtistText`（默认 `string.Empty`），在 `FromFileWithTags` 里填 `AppLang.LibraryUnknownAlbum` / `LibraryUnknownArtist` 作为空值回落 |
| 10 | `a5643ab` | 2026-10-20 | saillill | Music library: titles file under a pinyin-aware A-Z index | `LibraryIndexLetter.cs`(+198)、`PinyinInitial.cs`(+155)、`Modules/Common/View/IndexLetterOpacityConverter.cs`(+27)、`MpvPlayerPage.xaml`(+50)、`MpvPlayerPage_Library.cs`(+110/−7)、`NativeMethods.txt`(+3)、`mpv-conf-test/LibraryIndexLetterTests.cs`(+136) | 新增 `x:Name="LibraryIndexRail"` 与 `BuildLibraryIndexRail`/`BuildRail`/`FoldLatin`/`IsHan`/`LibraryIndexBucket`；新增 `x:Key="IndexLetterOpacity"` 转换器；`NativeMethods.txt` +3（pinyin 相关原生入口） |
| 11 | `d1fb2f5` | 2026-10-20 | saillill | Cards: corner hover buttons, a tick box, and a selection bar - one layer for all three kinds | `LibraryEntry.Selection.cs`(+93)、`LibraryEntry.cs`(+16/−1)、`MpvPlayerPage.xaml`(+170/−26)、`MpvPlayerPage_Library.cs`(+199/−5)、`AppLang.Shell.cs`(+20)、9 个 `Languages/*.json` 各 +7/−1 | 新增 10 个 `x:Name`（`CardSelectionBox`、`LibrarySelectionBar`、`LibrarySelectionCount`、`LibrarySelectionClearText`、`SelectionPlay/Queue/Properties` 按钮与文本…），删除 `x:Name="CardPlayText"`；新增 `SetSelectionMode`、`ToggleSelection`、`UpdateSelectionBar`、`ApplySelectionStrings`；新增 5 个 `lang.LibrarySelection*`/`lang.LibraryCardSelect` 键；遮罩色 `#66000000` → `#59000000` |
| 12 | `a57ea75` | 2026-10-20 | saillill | Music library: album cards are square, with the album and its artist | `MpvPlayerPage.xaml`(+131)、`MpvPlayerPage_Library.cs`(+46/−6)、`LibraryEntry.cs`(+8) | 新增 `x:Name` `AlbumCardHoverOverlay`、`AlbumCardMoreButton`、`AlbumCardPlayButton`、`AlbumCardSelectionBox`；新增 `AlbumCardLayout()`；新增 `StaticResource` `LibraryCardButtonStyle`、`LibraryCardMediaStyle`、`MpvMediaTileCaptionMargin`、`MpvSpaceXxs` |
| 13 | `76ffd54` | 2026-10-20 | saillill | Settings: a library could be bound but never unbound - the row, and the refresh behind it | `MpvPlayerPage_Library.cs`(+30)、`MpvPlayerPage.xaml.cs`(+12)、`SettingsPage.Options.PathFolders.cs`(+85)、`SettingsPage.Options.cs`(+17/−7)、`SettingsSections.cs`(+4)、`AppLang.cs`(+9)、`AppLang.Shell.cs`(+22)、9 个 `Languages/*.json` 各 +8/−1 | 新增 `ReloadLibraryForFolderChange()`；新增 4 个 `lang.Settings*LibraryFolders*` 键；新增设置分区文件 `SettingsPage.Options.PathFolders.cs` |
| 14 | `b0c6c31` | 2026-10-20 | saillill | cleanup: the first zero-risk batch, and three corrections to my own measurements | `MpvPlayerPage_Library.cs`(+27)、`MpvPlayerPage.xaml`(+3/−3)、`MpvPlayerPage_MpvMenu.cs`(−1)、`MpvConfOptionControl.xaml`(±1)、`MpvSoftwarePreviewControl.xaml`(±1)、`PiPWindow.xaml`(±1)、`ResizeGrip.xaml`+`.cs`(删除，−25) | 库重置路径新增清空：`_libraryThumbnails.Clear()`、`_libraryWithoutThumbnail.Clear()`、`_groupHeaders.Clear()`、`_libraryAll = []`、`_albumTracks = []`、`_lyrics = []`、`_palette = AlbumColorExtractor.None`、`_libraryLoaded = false`、`_activeCardOverlay = null`、`_cardMenuEntry = null`、`_openAlbum = null`；删除 `Modules/Player/ResizeGrip.xaml(.cs)` |
| 15 | `d82dc25` | 2026-10-21 | saillill | music: the artwork wash offsets from the theme instead of replacing it | `MpvPlayerPage_Music.cs`(+47/−3)、`Audio/ArtworkPalette.cs`(+37)、`Styles/Styles.xaml`(+17) | `MusicPageWash.Fill`/`MusicArtworkGlow.Fill` 从 `ArtworkPalette.Brush(_palette.Surface/Accent)` 改为走 `Tinted()`/`ThemeColor()`；`Styles.xaml` +17 行（17 个新增行里含主题色键） |
| 16 | `16906bf` | 2026-10-21 | saillill | settings: split the two 100-row folders, and show the letter rail videos were supposed to have | `MpvPlayerPage.xaml`(+14/−10)、`MpvPlayerPage_Library.cs`(+134/−28)、`SettingsPage.Options.Gen.cs`(+3564/−3536)、`SettingsPage.Sections.Gen.cs`(+196/−161)、`SettingsSections.cs`(+20/−1)、`AppLang.Sections.Gen.cs`(+16/−2)、9 个 `Languages/*.json` 各 +16/−2、`tools/gen-schema-options.py`(+160/−14) | 视频文件夹分组里把 `_groupHeaders.Clear()` 与新 `header` 变量纳入 `GroupVideoFolders` 循环，带 `group.Letter` 的分组写进 `_groupHeaders`；XAML 两处 `FontIcon` 加 `FontFamily="{StaticResource MpvIconFontFamily}"`，删除 `CardOverlayButton_PointerPressed` 方法 |
| 17 | `7a3bc6d` | 2026-10-22 | saillill | settings: the startup batch was never applied, and its commands were malformed | `MpvPlayerPage_Library.cs`(+6)、`MpvPlayerPage.xaml.cs`(+19/−3)、`MpvPlayerPage_PlaylistPanel.cs`(+2/−2)、`MpvPlayerPage_MenuBar.cs`(+2/−2)、`MpvPlayerExtensions.cs`(+11/−1)、`MpvSettings.cs`(+44/−1)、`Shell/MainWindow.xaml.cs`(+6)、`MpvPlayer.cpp`(+12/−1) | 库文件的改动是 4 行 `AutomationProperties.SetName(...)`：`VideoAddFolderButton`←`lang.LibraryAddFolder`、`VideoPlayAllButton`←`lang.LibraryPlayAll`、`QueueClearButton`←`lang.LibraryQueueClear`、`QueueAddToButton`←`lang.LibraryQueueAddTo` |
| 18 | `071d5d9` | 2026-10-24 | saillill | high contrast: the shell never followed it, for two separate reasons | `Styles/Styles.xaml`(+57)、`Modules/Common/View/WindowStyleManager.cs`(+21/−2)、`tools/check-resources.py`(+53/−1) | 新增 `ResourceDictionary x:Key="HighContrast"` 及其中约 10 个键（`MpvPlayControlBackground`、`MpvControlBackground`、`MpvShellPaneBrush`、`MpvShellBarBrush`、`MpvDropOverlayBrush`、`MpvMusicPageBrush`、`MpvMusicArtworkFallbackBrush`…）；`NavContentBackGroundColor`/`NavContentBorderColor` 改指向 `SystemColor*` |
| 19 | `74ad169` | 2026-10-26 | saillill | logging: make the four measurement blind spots stop lying | `MpvPlayerPage_Library.cs`(+25)、`MpvPlayerPage.xaml.cs`(+53)、`MpvPlayerPage_Info.cs`(+29/−1)、`MpvPlayerPage_PlayerView.cs`(+15/−1)、`PlayChain.cs`(+92)、`MpvPlayerExtensions.cs`(+9)、`MpvPlayer.cpp`(+131/−5)、`MpvPlayer.h`(+7/−1)、`LoggerHelper.cs`(+15/−1)、`SettingValueCodec.cs`(+139)、`CachedDataSetting.cs`(+7/−49)、`UnpackageAppDataSetting.cs`(+4/−52)、`mpv-conf-test/SettingValueCodecTests.cs`(+126) | 新增 `PlayChain.cs`（`PlayChain.Page(...)`）；库文件加 3 处 `PlayChain.Page` 调用（start-file handler、video/library 分支、`SetLibraryVisible` 的每个跃迁）；`CachedDataSetting.cs` 与 `UnpackageAppDataSetting.cs` 大幅删行，`SettingValueCodec.cs` 新增 |
| 20 | `466f11e` | 2026-10-26 | saillill | player: subscribe FileLoaded before the first file, not after it | `MpvPlayerPage.xaml.cs`(+65/−27) | 新增 `SubscribePerLoadEvents()` 与字段 `_perLoadEventsSubscribed`；`FileStarted += MpvPlayerPage_LibraryFileStarted`、`FileLoaded += MpvPlayerPage_PlayChainFileLoaded`、`FileLoaded += MpvPlayerPage_ApplyHdrModeOnce` 三行从 `Loaded` 处理路径移入该方法 |
| 21 | `1b49ab9` | 2026-10-26 | saillill | ui: put 首页 打开文件 on the heading row, where every other section's action is | `MpvPlayerPage_Library.cs`(+54/−5)、`Shell/MainWindow.xaml.cs`(+17/−6)、`tools/mvpw-mcp/verify_home_button.py`(+183) | 删除 `VideoAddFolderText.Text = lang.LibraryAddFolder;` 与对应的 `AutomationProperties.SetName(VideoAddFolderButton, …)` 两行（注释说明由 `ApplySectionHeader` 拥有）；`VideoAddFolderButton.Visibility` 的条件从 `(isVideos && hasVideoFolders) || (isMusic && hasMusicFolders)` 扩展到含 Home 分支 |
| 22 | `c798c6e` | 2026-10-26 | saillill | ui: the album card's captions were bound to nothing, and now render | `MpvPlayerPage.xaml`(+11/−4)、`MpvPlayerPage_Library.cs`(+175)、`tools/icon-standard-baseline.txt`(+16/−16)、`tools/typography-baseline.txt`(+4/−4) | 新增 4 个 `x:Name`（`AlbumCardTitleText`、`AlbumCardArtistText`、`AlbumCardCoverImage`、`AlbumCardFallbackIcon`），删除 `Source="{x:Bind Thumbnail, Mode=OneWay}"`；新增 `SetCardCaption`、`SetCardCover` |
| 23 | `52631b3` | 2026-10-26 | saillill | ui: the album and artist cards draw their cover, proved by a verifier that can see it | `MpvPlayerPage.xaml`(+4/−1)、`MpvPlayerPage_Library.cs`(+43)、`tools/mvpw-mcp/verify_music_cards.py`(+202)、`tools/check-baseline-lines.py`(+133) | 新增 `x:Name="AlbumCardMediaBorder"` 与 `SetCardMediaSquare(card)`，在模板求值后从代码设置媒体区高度（替代自引用的 `Height="{Binding ActualWidth, RelativeSource=Self}"`） |
| 24 | `db34c78` | 2026-10-26 | saillill | ui: the video library no longer shows an alphabet rail | `MpvPlayerPage_Library.cs`(+10/−2)、`tools/mvpw-mcp/verify_letter_rail.py`(+262) | `isGrouped` 的三元表达式 `_librarySection == LibrarySectionMusic ? _musicGrouping != MusicGrouping.Songs : _videoFolderView` 改为 `_librarySection == LibrarySectionMusic && _musicGrouping != MusicGrouping.Songs`（视频分支被移除） |
| 25 | `741d9b7` | 2026-10-26 | saillill | ui: the play queue gets its top-right open-file button | `MpvPlayerPage.xaml`(+42/−4)、`MpvPlayerPage_Library.cs`(+8)、`MpvPlayerPage_Playlist.cs`(+22)、`tools/icon-standard-baseline.txt`(+9/−9)、`tools/mvpw-mcp/verify_queue_button.py`(+172) | 队列头部由 `StackPanel` 改为三列 `Grid`；新增 `x:Name` `QueueOpenFileButton`、`QueueOpenFileIcon`、`QueueOpenFileText` 与 `QueueOpenFile_Click`；新增 `StaticResource MpvActionButtonPaddingTight` |
| 26 | `079e69d` | 2026-10-26 | saillill | ui: the letter rail grows its letters on hover, and this harness cannot see it happen | `MpvPlayerPage.xaml`(+2/−1)、`MpvPlayerPage_Library.cs`(+141)、`tools/mvpw-mcp/probe_rail_hover_wave.py`(+193) | 字母按钮加 `PointerEntered="IndexRailLetter_PointerEntered"`；新增 `ApplyRailHover`、`FindAncestorRepeater`、`RailEntryCount`、`IndexRailLetter_PointerEntered` |
| 27 | `c243b34` | 2026-10-26 | saillill | ui: the video library's folder view draws a cover per folder | `MpvPlayerPage.xaml`(+32/−9)、`MpvPlayerPage_Library.cs`(+21)、`tools/mvpw-mcp/verify_video_folder_cover.py`(+217) | 新增 `x:Name="FolderCoverImage"`，文件夹行头的 `Viewbox` 处改为 64×36 的 16:9 封面（注释记录：改动前该视图的自动化树里 `ImageControl` 元素数为 0） |
| 28 | `be32ec3` | 2026-10-26 | saillill | ui: the rail's hover wave is now arithmetic that can be tested, and it had a real bug | `IndexRailHover.cs`(+105)、`MpvPlayerPage_Library.cs`(+22/−50)、`mpv-conf-test/IndexRailHoverTests.cs`(+155) | 悬停波浪的算术从 `MpvPlayerPage_Library.cs` 内的私有常量（`RailHoverPeakScale = 1.9` 等）提取到 `IndexRailHover.cs`（`StrengthAt`、`ScaleAt`、`OpacityAt`、`Distance`），库文件相应删 50 行，新增 13 个测试方法 |
| 29 | `9a6096d` | 2026-10-26 | saillill | ui: the song card's cover never loaded, and the card menu was a font glyph from the wrong font | `MpvPlayerPage.xaml`(+23/−10)、`MpvPlayerPage_Library.cs`(+31/−7)、`tools/icon-standard-baseline.txt`(+15/−17)、`tools/typography-baseline.txt`(+7/−7) | 新增 `x:Name` `SongCardCoverImage`、`SongCardFallbackIcon`，删除 `StaticResource MpvIconFontFamily`（卡片菜单字形改用另一字体） |
| 30 | `0d0e889` | 2026-10-26 | saillill | ui: returning to a playing track restores the music page | `MpvPlayerPage_Library.cs`(+12) | `SetLibraryVisible` 的 `if (!wasVisible)` 早退分支里，`LibraryHost.Visibility = Collapsed;` 之后新增 `ShowMusicPageFor(_mediaPlayer?.GetCurrentPath());` |

**30 行 = 30 个提交，无遗漏、无重复。** 复核：表格行数即 §2.2 的并集大小。

### 2.5 这 30 个提交按日分布

| 日期 | 提交数 | 提交 |
|---|---|---|
| 2026-10-19 | 2 | `3017c3c` `37a649c` |
| 2026-10-20 | 12 | `1066ffe` `19cfeb5` `8cb9f27` `05befbf` `7cab636` `b485adf` `dc63a7d` `a5643ab` `d1fb2f5` `a57ea75` `76ffd54` `b0c6c31` |
| 2026-10-21 | 2 | `d82dc25` `16906bf` |
| 2026-10-22 | 1 | `7a3bc6d` |
| 2026-10-24 | 1 | `071d5d9` |
| 2026-10-26 | 12 | `74ad169` `466f11e` `1b49ab9` `c798c6e` `52631b3` `db34c78` `741d9b7` `079e69d` `c243b34` `be32ec3` `9a6096d` `0d0e889` |
| **合计** | **30** | 2+12+2+1+1+12 = 30，与 §2.4 的 30 行一致 |

说明：`0b17cc0` 本身（2026-10-19 21:28:32）**不在**这 30 行里，它在窗口之外。2026-10-19 当天的 `3017c3c`(23:21:00)、`37a649c` 都在它之后，已用 `git merge-base --is-ancestor 0b17cc0 <sha>` 逐个确认。

```bash
git merge-base --is-ancestor 0b17cc0 3017c3c && echo yes   # yes
git merge-base --is-ancestor 0b17cc0 37a649c && echo yes   # yes
```

---

## 3. 检索用查找表

### 3.1 窗口内 30 个提交 → 文件（机器可读）

格式：`sha<TAB>日期<TAB>文件（只列主页/库相关，`A/`= `mpv-winui/mpv-winui/`，`P/`= `Modules/Player/`）`。

```text
3017c3c  2026-10-19  P/MpvPlayerPage_Music.cs
37a649c  2026-10-19  P/LibraryEntry.cs; P/LibraryTagsReader.cs; P/MusicTags.cs; P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
1066ffe  2026-10-20  P/Audio/AlbumColorExtractor.cs; P/Audio/ArtworkPalette.cs; P/Audio/AudioCapture.cs; P/Audio/SpectrumAnalyzer.cs; P/LyricsLoader.cs; P/MpvPlayerPage.xaml; P/MpvPlayerPage_Music.cs
19cfeb5  2026-10-20  P/MpvPlayerPage_Library.cs; P/PlayerControl.Animation.cs; P/PlayerControl.PanelAnimation.cs; P/PlayerControl.xaml.cs
8cb9f27  2026-10-20  P/MpvPlayerPage_Library.cs
05befbf  2026-10-20  A/Shell/MainWindow.xaml.cs
7cab636  2026-10-20  A/Modules/Common/View/AspectHeightConverter.cs; P/MpvPlayerPage.xaml
b485adf  2026-10-20  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
dc63a7d  2026-10-20  P/LibraryEntry.cs
a5643ab  2026-10-20  A/Modules/Common/View/IndexLetterOpacityConverter.cs; P/LibraryIndexLetter.cs; P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs; P/PinyinInitial.cs
d1fb2f5  2026-10-20  P/LibraryEntry.Selection.cs; P/LibraryEntry.cs; P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
a57ea75  2026-10-20  P/LibraryEntry.cs; P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
76ffd54  2026-10-20  P/MpvPlayerPage.xaml.cs; P/MpvPlayerPage_Library.cs
b0c6c31  2026-10-20  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
d82dc25  2026-10-21  P/Audio/ArtworkPalette.cs; P/MpvPlayerPage_Music.cs; A/Styles/Styles.xaml
16906bf  2026-10-21  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
7a3bc6d  2026-10-22  P/MpvPlayerExtensions.cs; P/MpvPlayerPage.xaml.cs; P/MpvPlayerPage_Library.cs; P/MpvPlayerPage_PlaylistPanel.cs; A/Shell/MainWindow.xaml.cs
071d5d9  2026-10-24  A/Styles/Styles.xaml; A/Modules/Common/View/WindowStyleManager.cs
74ad169  2026-10-26  P/MpvPlayerExtensions.cs; P/MpvPlayerPage.xaml.cs; P/MpvPlayerPage_Library.cs; P/MpvPlayerPage_Info.cs; P/MpvPlayerPage_PlayerView.cs; P/PlayChain.cs
466f11e  2026-10-26  P/MpvPlayerPage.xaml.cs
1b49ab9  2026-10-26  P/MpvPlayerPage_Library.cs; A/Shell/MainWindow.xaml.cs
c798c6e  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
52631b3  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
db34c78  2026-10-26  P/MpvPlayerPage_Library.cs
741d9b7  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs; P/MpvPlayerPage_Playlist.cs
079e69d  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
c243b34  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
be32ec3  2026-10-26  P/IndexRailHover.cs; P/MpvPlayerPage_Library.cs
9a6096d  2026-10-26  P/MpvPlayerPage.xaml; P/MpvPlayerPage_Library.cs
0d0e889  2026-10-26  P/MpvPlayerPage_Library.cs
```

（`MpvPlayerPage_Playlist.cs` 只出现在 `741d9b7` 一行：它在窗口内的另外 3 次改动 —— `0129738`、`8540cf9`、`3b662a8` —— 不在那 30 个之内，因为 `MpvPlayerPage_Playlist.cs` 不在 §2.2 A 层的 10 个文件里。这是口径的已知边界，显式写出。）

### 3.2 高流量文件 → 提交索引（6 个文件，全历史）

每格：`sha 日期`。判断「谁改过这个文件」时看这一节，不要看 §2。

#### `mpv-winui/mpv-winui/Shell/MainWindow.xaml` — 10 次（新路径 2 ∪ 旧路径 9 − 重叠 1）

| 时期 | sha / 日期 |
|---|---|
| `Shell/` 之后（2） | `0b17cc0` 2026-10-19（+497/−11，本文件单次最大）· `144650a` 2026-09-17（`R100` 移入 `Shell/`，内容未变） |
| `Shell/` 之前（9，以旧路径计） | `3f4b3fa` 2026-07-15 · `ec0e636` 2026-07-15 · `4bbb1fd` 2026-07-15 · `4d7dc8b` 2026-07-24 · `aa94923` 2026-08-24 · `4101787` 2026-08-28 · `18d3b80` 2026-09-01 · `cc98273` 2026-09-13（2 父合并）· `144650a` 2026-09-17 |

```bash
git log --full-history --format='%h %ad %s' --date=short -- 'mpv-winui/mpv-winui/MainWindow.xaml'   # 9
git log --format='%h %ad %s' --date=short -- 'mpv-winui/mpv-winui/Shell/MainWindow.xaml'            # 2
git log --follow --format='%h %ad %s' --date=short -- 'mpv-winui/mpv-winui/Shell/MainWindow.xaml'   # 9（另一种口径，集合不同）
```

> 旧路径不加 `--full-history` 只给 6 条，会漏掉 `aa94923`、`4101787`（被历史简化跳过）与 `cc98273`（合并提交）。`--follow` 数字上也是 9，但集合不同：含 `0b17cc0`、不含 `cc98273`。

#### `mpv-winui/mpv-winui/Shell/MainWindow.xaml.cs` — 35 次（新路径 7 ∪ 旧路径 29 − 重叠 1）

| 时期 | 要点 |
|---|---|
| `Shell/` 之后（7） | `1b49ab9` 2026-10-26（+17/−6）· `836bb39` 2026-10-24 · `7a3bc6d` 2026-10-22（+6）· `9f27024` 2026-10-22（`a11y: give each nav item an AutomationId, so a test can find it in any language`）· `05befbf` 2026-10-20（+43/−8）· `0b17cc0` 2026-10-19（+768/−7，本文件单次最大）· `144650a` 2026-09-17（`R100` 移入） |
| `Shell/` 之前（29） | 以旧路径 `mpv-winui/mpv-winui/MainWindow.xaml.cs` 计，共 29 条；其中最大的一条是 `f580aee` 2026-07-26（add changing theme support） |

```bash
git log --full-history --format='%h %ad %s' --date=short -- 'mpv-winui/mpv-winui/MainWindow.xaml.cs'   # 29
git log --format='%h %ad %s' --date=short -- 'mpv-winui/mpv-winui/Shell/MainWindow.xaml.cs'            # 7
```

> 合计 35 = 7 + 29 − 1：`144650a`（`R100` 移入）同时出现在两段里，减一次。`git log --follow` 给 34，因为它对 2 父合并 `cc98273` 的处理与 `--full-history` 不同，见本节末尾的口径表。

#### `mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs` — 23 次（22 + 引入它的 `0b17cc0`）

全部集中在 2026-10-19 之后。第一行是引入该文件的提交，不在 §2 窗口内。

| sha | 日期 | 该文件的增删 |
|---|---|---|
| `0b17cc0` | 2026-10-19 | +1898（新增文件；在 §2.2 窗口之外） |
| `37a649c` | 2026-10-19 | +444/−14 |
| `19cfeb5` | 2026-10-20 | +36/−10 |
| `8cb9f27` | 2026-10-20 | +40/−2 |
| `b485adf` | 2026-10-20 | +55/−16 |
| `a5643ab` | 2026-10-20 | +110/−7 |
| `d1fb2f5` | 2026-10-20 | +199/−5 |
| `a57ea75` | 2026-10-20 | +46/−6 |
| `76ffd54` | 2026-10-20 | +30 |
| `b0c6c31` | 2026-10-20 | +27 |
| `16906bf` | 2026-10-21 | +134/−28 |
| `7a3bc6d` | 2026-10-22 | +6 |
| `74ad169` | 2026-10-26 | +25 |
| `1b49ab9` | 2026-10-26 | +54/−5 |
| `c798c6e` | 2026-10-26 | +175 |
| `52631b3` | 2026-10-26 | +43 |
| `db34c78` | 2026-10-26 | +10/−2 |
| `741d9b7` | 2026-10-26 | +8 |
| `079e69d` | 2026-10-26 | +141 |
| `c243b34` | 2026-10-26 | +21 |
| `be32ec3` | 2026-10-26 | +22/−50 |
| `9a6096d` | 2026-10-26 | +31/−7 |
| `0d0e889` | 2026-10-26 | +12 |

`d82dc25` 不在此表：该提交只改 `Styles.xaml`、`MpvPlayerPage_Music.cs`、`Audio/ArtworkPalette.cs`，不改这个文件（核：`git show --name-only --format='' d82dc25`）。

```bash
git log --full-history --format='@@%h %ad' --date=short --numstat -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs
```

#### `mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml` — 72 次（2026-07-15 .. 2026-10-26）

单次最大的是 `0b17cc0`（+1378/−203）。§2 窗口内有 15 个提交改过它（§3.1 里凡是列出 `P/MpvPlayerPage.xaml` 的行）。

```bash
git log --full-history --format='%h %ad %s' --date=short -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml | wc -l   # 72
```

#### `mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml.cs` — 71 次（2026-07-15 .. 2026-10-26）

```bash
git log --full-history --format='%h %ad %s' --date=short -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml.cs | wc -l   # 71
```

#### `mpv-winui/mpv-winui/Styles/Styles.xaml` — 41 次（2026-07-15 .. 2026-10-24）

三个最大的单次改动，都在 §2 窗口内：

| sha | 日期 | 该文件的增删 |
|---|---|---|
| `071d5d9` | 2026-10-24 | +57（`x:Key="HighContrast"` 字典） |
| `d82dc25` | 2026-10-21 | +17 |
| `0b17cc0` | 2026-10-19 | +422/−31（本文件单次最大） |

```bash
git log --full-history --format='%h %ad %s' --date=short -- mpv-winui/mpv-winui/Styles/Styles.xaml | wc -l   # 41
git log --format='%h %ad' --date=short --numstat -- mpv-winui/mpv-winui/Styles/Styles.xaml | sort -rn | head -4
```

#### §3.2 的计数口径（必须和数字一起读）

这一节第一版算错了，原因是**三种 `git log` 数法对同一个文件给出不同答案**。实测（`nameonly` = §0.1 的 `--name-only` 清单；`default` = `git log -- <path>`；`full` = `git log --full-history -- <path>`；`follow` = `git log --follow -- <path>`）：

| 文件 | nameonly | default | full | follow | **本表采用** |
|---|---|---|---|---|---|
| `Shell/MainWindow.xaml` | 2 | 2 | 2 | 9 | **10**（新 2 ∪ 旧 9 − 重叠 1） |
| `Shell/MainWindow.xaml.cs` | 7 | 7 | 7 | 34 | **35**（新 7 ∪ 旧 29 − 重叠 1） |
| `Modules/Player/MpvPlayerPage_Library.cs` | 23 | 23 | 23 | 23 | **23** |
| `Modules/Player/MpvPlayerPage.xaml` | 71 | **51** | **72** | 71 | **72** |
| `Modules/Player/MpvPlayerPage.xaml.cs` | 70 | 71 | 71 | 70 | **71** |
| `Styles/Styles.xaml` | 40 | 41 | 41 | 40 | **41** |

两种偏差，原因不同：

1. **`default` 会少算**（`MpvPlayerPage.xaml`：51 vs 72）。默认的历史简化会跳过合并提交另一侧分支上的改动。加 `--full-history` 关闭简化后是 72。
2. **`--follow` 会多算也会少算。** 它跨重命名追溯，所以 `MainWindow.xaml` 从 2 变 9（把 `Shell/` 之前的 7 条拿回来，其中 `4101787`、`aa94923` 两条连 `-- <旧路径>` 也查不到）；它同时漏掉 2 父合并 `cc98273`，所以 `MpvPlayerPage.xaml` 是 71 而不是 72。

本表因此**不用单一命令**，而是「新路径 `--full-history` ∪ 旧路径 `--full-history`，减去重叠」，每一格下面都写了所用命令。

```bash
# default 与 full 的差
git log               --format='%h' -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml | wc -l   # 51
git log --full-history --format='%h' -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml | wc -l   # 72

# follow 与 full 的差
git log --follow       --format='%h' -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml | wc -l   # 71
git log --follow       --format='%h' -- mpv-winui/mpv-winui/Shell/MainWindow.xaml | wc -l              # 9
git log --full-history --format='%h' -- mpv-winui/mpv-winui/Shell/MainWindow.xaml | wc -l              # 2

# two hidden commits that only --follow reaches
git log --follow --format='%h %ad %s' --date=short -- mpv-winui/mpv-winui/Shell/MainWindow.xaml | grep -E '4101787|aa94923'
```
### 3.3 `git log -S` / `git log -G` 配方（实际跑过的命令原文）

全部在仓库根执行。注释里的数字是**实测输出**，可直接核对。

**(1) 某个 `x:Name` 是哪次提交引入的** —— 用 `-S`，因为这里关心「字符串从无到有」：

```bash
git log --format='%h %ad %s' --date=short -S 'QueueOpenFileButton'
# 1 条：741d9b7 2026-10-26 ui: the play queue gets its top-right open-file button
```

> 坑：`-S` 的参数必须带引号或空格。写成 `-S'QueueOpenFileButton'` 在某些 shell 下被吞成 `-S` 无参，实测会返回 17 条（等于「所有提交」）。用 `-S 'QueueOpenFileButton'` 或 `--pickaxe-regex -S 'QueueOpenFileButton'` 才是 1 条。

**(2) 某个 `x:Name` 的所有改动历史** —— 用 `-S` 全历史：

```bash
git log --format='%h %ad %s' --date=short -S 'LibrarySectionSubtitle'
# 3 条（0b17cc0 引入 + 2 次后续修改）
```

**(3) 哪些提交动过库标识符（B 层的判定命令）**：

```powershell
git log --format='%h %ad %s' --date=short `
  -G'Library[A-Z]|Music[A-Z]|Album[A-Z]|Song[A-Z]|Artist[A-Z]|Folder[A-Z]|Queue[A-Z]' `
  -- 'mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml' `
     'mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml.cs' `
     'mpv-winui/mpv-winui/Styles/Styles.xaml' `
     'mpv-winui/mpv-winui/Shell/MainWindow.xaml' `
     'mpv-winui/mpv-winui/Shell/MainWindow.xaml.cs'
# 窗口内 17 条
```

**(4) 哪些提交动过库的核心类型**（A 层判定，26 条）：

```powershell
git log --format='%h %ad %s' --date=short 0b17cc0..HEAD -- `
  'mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs' `
  'mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Music.cs' `
  'mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_PlaylistPanel.cs' `
  'mpv-winui/mpv-winui/Modules/Player/LibraryEntry.cs' `
  'mpv-winui/mpv-winui/Modules/Player/LibraryEntry.Selection.cs' `
  'mpv-winui/mpv-winui/Modules/Player/LibraryIndexLetter.cs' `
  'mpv-winui/mpv-winui/Modules/Player/LibraryTagsReader.cs' `
  'mpv-winui/mpv-winui/Modules/Player/MusicTags.cs' `
  'mpv-winui/mpv-winui/Modules/Player/PinyinInitial.cs' `
  'mpv-winui/mpv-winui/Modules/Player/IndexRailHover.cs'
```

**(5) 库 section 的常量从哪来**（`-S` 全历史，5 条）：

```bash
git log --format='%h %ad %s' --date=short -S 'LibrarySectionHome'
# 0f70f74 2026-10-26 docs: alignment baseline, measured on 0d0e889
# 1b49ab9 2026-10-26 ui: put 首页 打开文件 on the heading row ...
# bad0d40 2026-10-22 domains: fourteen context entries, and the index that points at them
# 05befbf 2026-10-20 Shell: the open-file button gets one owner, and the owner knows the section
# 0b17cc0 2026-10-19 Shell and player: home, two libraries, music page, queue popup, transport
```

> 注意 `0f70f74` 只在 `docs/` 里引用了这个字符串（不在代码里），`bad0d40` 只在 `DOMAINS.md` 里引用。`-S` 不带 pathspec 时会把文档一起算进去。要只看代码：加 `-- '*.cs' -- '*.xaml'`。

**(6) 某个 section 常量在代码里的全部出现位置**：

```bash
git grep -n 'LibrarySectionHome' HEAD -- '*.cs'
# mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs:57:  internal const string LibrarySectionHome = "home";
# mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs:101: private string _librarySection = LibrarySectionHome;
# （其余略，总数见命令输出）
```

**(7) 某个文件在某窗口内被改了几次**：

```bash
git log --oneline 0b17cc0..HEAD -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs | wc -l   # 22
git log --oneline --all -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage_Library.cs | wc -l            # 23
```

（22 vs 23 的差：`0b17cc0` 自身引入该文件，但它在窗口之外。）

**(8) 一次提交在某域的净行数**（用于 §1.3 的「最热文件」与代表提交挑选）：

```bash
git show --numstat --format='' 0b17cc0 | awk '{a+=$1; d+=$2} END {print a" added, "d" deleted"}'
# 10630
```

**(9) 重命名前后的路径断层**（§0.2 那条事实的检查命令）：

```bash
git show --name-status -M 144650a | grep '^R'          # 6 条 R100 MainWindow* -> Shell/MainWindow*
git log --format='%h' --follow -- mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml | wc -l   # 71（--follow 无跨路径可跟）
```

**(10) 主页 section 的 XAML 锚点**：

```bash
git grep -n 'x:Name="LibraryHost"\|x:Name="LibraryGrid"\|x:Name="LibrarySectionSubtitle"' HEAD -- '*.xaml'
# mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml:1085  x:Name="LibraryHost"
# mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml:1208  x:Name="LibrarySectionSubtitle"
# mpv-winui/mpv-winui/Modules/Player/MpvPlayerPage.xaml:1397  x:Name="LibraryGrid"
```

---

## 4. 未能确定的事项（显式列出，不粉饰）

1. **`0b17cc0` 是否真的「引入」了主页，本文件只按它新增的文件与标题判断。** 该提交标题含 `home, two libraries, music page, queue popup, transport`，且新增了 `MpvPlayerPage_Library.cs`（其中 `LibrarySectionHome = "home"` 在第 57 行）。但**没有**读到提交之前是否存在一个 `home` 的实现被替换，因此「主页在这一天之前不存在」是**未判定**。可核：`git ls-tree -r --name-only 0b17cc0^ -- mpv-winui/mpv-winui/Modules/Player | grep -i librar`（预期为空）。

2. **`MpvPlayerPage_Playlist.cs` 是否属于「媒体库」。** 它 24 次改动、承载队列面板的一部分，但本索引未把它列进 A 层（列进去会让并集从 30 变成 32，多出 `0129738`、`8540cf9`）。取舍依据是任务给的域描述里「队列」属 Player 但本索引的主页/库窗口以 `Library*` 文件为主。**这是口径选择，不是事实判断。**

3. **四个合并提交的真实改动范围。** `9fe29fb`、`c5ffaf8`、`f664e84`、`cc98273` 在 `--name-only` 下文件数为 0。用 `-m` 展开可以拿到，但那会把同一合并按父重复展开，计数不可复核，本索引未采用。`cc98273`（`merge: adopt upstream player architecture (WIP, does not build yet)`）的实际改动量因此**未计入任何域**。

4. **`Modules/Common/`、`Modules/FileSystem/`、`Modules/Activation/`、`Modules/ActivationRegistration/`、`Modules/About/`、`Modules/AppModel/`、`Modules/MediaInfo/`、`Modules/Motion/`、`Modules/Mpv/` 的归属未定。** 共约 90 个文件，贡献的提交落在 §1.4。没有把它们并入 Player 是刻意的（会污染 Player 的可复核性），但这也意味着这 31 个未归域提交里有相当一部分其实是 app 代码。

5. **`mpv-winui/mpv-winrt/`（C++/WinRT）完全在索引之外。** 它对 733 的贡献约为 19 个提交（见 §1.4），但本索引的八域不含原生层。若需要，`git log --format='%h %ad %s' --date=short -- mpv-winui/mpv-winrt` 可以单独成表；本文件没做。

6. **`docs/` 的内容未被核对，除两份 baseline。** 因此 §1.5 Docs 域里对单个提交的「为什么代表」只依据行数与文件数，不依据文档说了什么。

7. **`0b17cc0` 与 `0d0e889` 之间的 151 个提交全部出自 `saillill` 一人**，窗口内无第二作者。这不影响索引，但意味着「谁改的」这一问题在这段窗口里没有区分度。

8. **`git log --numstat` 在重命名提交上给出的是重命名记法**（`mpv-winui/mpv-winui/{ => Shell}/MainWindow.xaml`），且增删为 `0 0`。本索引 §1.5 里凡引用行数的地方都避开了重命名提交；`144650a` 只在 §1.5 Shell 表里作为「唯一一次目录重排」出现，未给行数。

---

## 5. 一句话用法

- 查「这个提交算哪个域」→ §1.1 的谓词 + §1.2 的脚本。
- 查「主页/媒体库那段发生了什么」→ §2.4 的 30 行表。
- 查「谁改过 `Styles.xaml`」→ §3.2。
- 查「某个 `x:Name` / 常量是哪来的」→ §3.3 的 `-S` 配方。
- 查「133 行里为什么是 30 不是 43」→ §2.2 的 A/B 两层与去重。
