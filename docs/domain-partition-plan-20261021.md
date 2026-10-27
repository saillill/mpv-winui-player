# mpvw 功能分区方案（Function-domain partitioning）

> **状态（2026-10-27）：已被取代，保留作历史记录。**
>
> 本文件是 2026-10-21 的 12 域草稿。当前采用的方案是
> `docs/prompt-v2/05-MODULE-DESIGN.md`（9 个功能域 + 3 个非域层）。
>
> **采纳了什么**：`Playback`/`Library`/`Playlist` 的拆分理由；
> "一个功能域 = 一个可独立阅读的目录"这一目标；以及 §6.1 关于"物理移动会牵动
> XAML 资源 URI 与 `mpv-conf-test` 显式 `Compile Include`"的成本警告
> （后来实测确认：18 条 `Compile Include` + 2 条 `Compile Remove`）。
>
> **否决了什么**：`Home`/`Media`/`Theme`/`Localization`/`Platform` 五个域。
> 实测这五个在代码里没有独立实体：`Home` 的逻辑在 `MpvPlayerPage_Library.cs` 内；
> `Theme` 只有 1 个 xaml + 1 个 manager；`Localization` 就是 `Language`；
> `Platform` 是层不是域。理由见 `05-MODULE-DESIGN.md` §2 与 §7。
>
> 本文件仍被 `mpv-winui/mpv-winui/DOMAINS.md` 与 `Modules/Mpv/README.md` 引用，
> 因此不删除。

**基线**：HEAD `7b42fd5`　**配套**：`docs/project-overview-20261021.md`
**目标**：让「一个功能域 = 一个可独立阅读的目录」，使人和 AI 都能只读该目录就接手该域，
不再因为 `Modules/Settings` 有 87 个文件、`Modules/Player` 有 79 个文件而在整个 `Modules/`
里反复翻找。

---

## 0. 现状问题（先说清为什么要分区）

| 现象 | 数字 | 后果 |
|---|---|---|
| 两个目录占掉全部源码的 2/3 | `Player` 79 文件 1040 KB，`Settings` 87 文件 1604 KB | 找任何东西都要在这两个目录里穷举 |
| 三个目录按"实现机制"命名而非按功能 | `Common`(33)、`Mpv`(3)、`Motion`(5) | 归属不明，任何域都可能往里加东西，最终变成杂物间 |
| 一个类被切成十几个 partial | `MpvPlayerPage` 23 个 partial，`SettingsPage` 14 个 | partial 之间没有边界说明，改一处要读十处 |
| 没有"域说明书" | 0 个分区 README | 新人/AI 只能靠读全部代码推断职责 |

---

## 1. 分区总图

```
mpv-winui/mpv-winui/
├─ App.xaml(.cs)  Program.cs            入口（根，不动）
├─ Shell/                              ← 域 0：外壳
├─ Modules/
│   ├─ Home/                           ← 域 1：主页
│   ├─ Library/                        ← 域 2：媒体库（音乐 + 视频，共享卡片/索引/选择）
│   │   ├─ Music/
│   │   └─ Video/
│   ├─ Player/                         ← 域 3：播放页（表面、控制栏、全屏、输入转发）
│   ├─ Playback/                       ← 域 4：播放队列 + 播放列表 + 历史/稍后
│   ├─ Media/                          ← 域 5：媒体信息 + 截屏 + 拖放 + 封面/标签
│   ├─ Settings/                       ← 域 6：设置窗口 + 选项树
│   ├─ MpvConf/                        ← 域 7：mpv.conf 读写/编辑/schema
│   ├─ Menu/                           ← 域 8：右键菜单 + 菜单编辑器 + 菜单栏
│   ├─ Theme/                          ← 域 9：主题 / 皮肤 / 视觉令牌
│   ├─ Localization/                   ← 域 10：AppLang + 语言文件
│   └─ Platform/                       ← 域 11：Win32/激活/文件系统/AppModel
├─ Infrastructure/                     ← 域 12：跨域共享（不是功能域）
│   ├─ Motion/                         动效（token/easing/spring/gate/behaviors）
│   ├─ Controls/                       通用控件扩展（CheckBoxExt/SliderExt/TickBar…）
│   ├─ Converters/                     值转换器
│   └─ Utils/                          atomic file、config write queue、hash、media kinds…
├─ Assets/  Languages/  Menus/  MpvConfOptions/  Styles/  Themes/   ← 资源（不动）
└─ Shell/                              （同上）
```

### 为什么这样切

- **按用户能说出的东西切，不按技术分层切**。用户会说"音乐库""设置页""播放页"，不会说
  "Common" 或 "Mpv"。域的边界应当与功能地图（总览报告 §2.2）一一对应。
- **`Library` 收成一个域、两个子目录**。音乐库和视频库共享 `LibraryEntry`、`LibraryIndexLetter`、
  `LibraryEntry.Selection`、卡片样式、选择栏——它们是**一套机制的两个实例**，不拆成两个域，
  否则共享物又要回到杂物间。
- **`Playback` 与 `Player` 分开**。播放页是"看视频的地方"，队列/历史/稍后是"数据集合的视图"，
  两者生命周期不同：关掉视频队列仍在。
- **`Infrastructure` 明确标注为"不是功能域"**。这解决了 `Common`/`Motion`/`Mpv` 三个杂物间的
  归属问题：它们是跨域基础设施，任何域都允许依赖，但**它们不允许依赖任何功能域**。

---

## 2. 每个分区的"上下文入口"

每个分区根目录放一个 `README.md`，固定五段（≤40 行）：

```markdown
# <域中文名> / <Domain>

## 职责
一句话说清这个域负责什么，以及明确不负责什么。

## 入口文件
- `XxxPage.xaml(.cs)` — 页面/视图入口
- `XxxService.cs` — 服务入口

## 对外接口
| 类型 | 成员 | 谁在用 |
|---|---|---|
| `public static class Xxx` | `DoSomething()` | Shell/MainWindow |

## 依赖
依赖：`Infrastructure/*`、`MpvConf`（只读）
**不依赖**：`Settings`、`Menu`（禁止反向）

## 关键状态
| 状态 | 存放 | 生命周期 |
|---|---|---|
| `_librarySection` | `MpvPlayerPage_Library.cs` 字段 | 页面存活期 |
| 库文件夹 | `AppSettings.VideoLibraryFolders`（注册表） | 持久 |
```

**现有可直接复用的材料**：`docs/state-ownership.md`（11821 B）已经把状态归属梳理过一遍，
分区 README 的"关键状态"一节应当从它裁剪，不要重新发明。

---

## 3. 依赖方向（硬规则）

```
        ┌──────────────────────────────────────────┐
        │  Shell（外壳：MainWindow / AppContext）     │
        └───────────────┬──────────────────────────┘
                        │ 只向下
        ┌───────────────▼──────────────────────────┐
        │  Views（Home / Library / Player / Playback │
        │        Media / Settings / MpvConf / Menu） │
        └───────────────┬──────────────────────────┘
                        │
        ┌───────────────▼──────────────────────────┐
        │  Services（Settings 持久化 / MpvConf /      │
        │            Localization / Theme / Menu 生成）│
        └───────────────┬──────────────────────────┘
                        │
        ┌───────────────▼──────────────────────────┐
        │  Transport（mpv_winrt 投影 + MpvCommandQueue）│
        └───────────────┬──────────────────────────┘
                        │
        ┌───────────────▼──────────────────────────┐
        │  Infrastructure（Motion/Controls/Converters/Utils）│
        └──────────────────────────────────────────┘
```

**允许的跨域引用（白名单）**：

| 从 | 到 | 理由 |
|---|---|---|
| 任意 View | `Shell/AppContext` | 唯一全局服务出口（已有） |
| `Player` | `Playback` | 播放页要画队列侧面板 |
| `Library` | `Media` | 卡片要取封面/标签 |
| `Settings` | 所有域的服务 | 设置页是"全应用的控制面板"，只读服务，不反向 |
| `Infrastructure` | 无 | **禁止**依赖任何功能域 |

**禁止**：`Player → Settings`（除 `AppSettings` 数据模型）、`Menu → Player`、`Theme → Settings`。

---

## 4. 分阶段实施（每阶段可独立构建验证）

### 阶段 A：只加说明书与显式边界（零移动，零风险）

1. 为 12 个域各写 `README.md`（新建文件，不改任何代码）。
2. 在 `AGENTS.md` 追加「分区表」一节，指向这些 README。
3. 新增检查器 `tools/check-domain-imports.py`：解析 `using` 语句，检查是否出现白名单外的
   跨域引用；**先让它对当前代码报红**（记录基线），再逐条修。

**验证**：`build.ps1` 0 错误 + 新检查器输出一份"当前越界清单"。

### 阶段 B：把基础设施从功能域里搬出去

| 从 | 到 | 文件 |
|---|---|---|
| `Modules/Motion/` | `Infrastructure/Motion/` | 5 个 |
| `Modules/Common/View/Controls/` | `Infrastructure/Controls/` | 4 个 |
| `Modules/Common/Convertor/` + `View/*Converter.cs` | `Infrastructure/Converters/` | 4 个 |
| `Modules/Common/Utils/` | `Infrastructure/Utils/` | 12 个 |
| `Modules/Common/View/`（余下：窗口/图标/主题资源） | `Shell/Chrome/`（图标、窗口样式）| 10 个 |
| `Modules/Mpv/` | `Modules/Platform/Win32/` | 3 个 |

**风险点（已核实）**：
- XAML 的 `ms-appx:///` URI 会随目录变化——但本工程用默认通配编译，`x:Class` 是全限定名，
  **移动只影响 XAML 里的 `using:` 命名空间前缀**，需要同步改。
- `mpv-conf-test` 用 `<Compile Include="..\mpv-winui\Modules\Motion\SpringMath.cs" Link=...>`
  显式链接源码，**必须同步改 csproj 路径**（这是最容易漏的一步）。
- `check-visual-state-layout.py` 硬编码了 `Modules/Player/*.xaml` 的路径，移动后要更新。

**验证**：`build.ps1` + 17 检查器 + 496/26 测试全绿。

### 阶段 C：拆大域（可选，按你的意见决定）

| 域 | 现状 | 建议拆分 | 收益 |
|---|---|---|---|
| `Player` | 79 文件 | `Player/Transport/`（控制栏 8 个）、`Player/Library/`→ 移到 `Library/`、`Player/PiP/`（4 个）、`Player/Menu/`→ 移到 `Menu/` | 每个子目录 ≤25 文件 |
| `Settings` | 87 文件 | 保持扁平（它的 partial 结构是**故意**的，见 `docs/settings-design-contract.md`） | 不动 |
| `Library` | — | `Library/Music/`、`Library/Video/` | 与用户用词一致 |

**我的建议**：阶段 A 立即做；阶段 B 做；**阶段 C 中 `Player/Library → Library` 和
`Player/Menu → Menu` 值得做，其余子目录拆分收益低**——因为 `MpvPlayerPage` 的 partial 命名
已经表达了归属（`_Music`、`_Playlist`、`_PiP`…），再套目录只是把同一件事说两遍。

---

## 5. 与云端仓库的关系

云端 `saillill/mpv-winui-player` 的 `Modules/` 结构与本地区别很小（云端 81 个 Settings 文件
vs 本地 87），**它没有分区方案**。所以本方案是**本地扩展**，不构成对云端的偏离；
反而因为本地是云端的严格后继（领先 116 提交、落后 0），后续从云端取东西时可以直接
`git cherry-pick`，分区不会阻碍比对。

---

## 6. 待你确认的三件事

1. **接受 A + B，C 只做两项？（推荐）** 还是 A/B/C 全做，或只做 A？
2. **`Infrastructure/` 这个名字可以吗？** 备选：`Foundation/`、`Shared/`。
3. **分区 README 用中文还是双语？** 项目代码注释是英文、文档是中文，我建议 README 用中文
   （读者是人），表格里的类型名保持英文。
