# docs 索引

本目录是 mpvw 的项目契约。每份文档只描述**当前代码里的事实**，不写计划、不写日期基线。
数字一律附重算命令；读不出原因的约束会标注「原因不明」。

## 文档职责

| 文件 | 规定什么 | 读者 |
|---|---|---|
| [`architecture.md`](architecture.md) | 四层与 8 个域的职责边界、禁止事项、实测依赖方向 | 准备跨目录改代码的人 |
| [`settings-contract.md`](settings-contract.md) | 设置界面：三层分工、命名与分组、验证流程、已知例外登记 | 改 `Domains/Settings`、`SettingsModel`、`MpvConf` 的人 |
| [`mpv-conf.md`](mpv-conf.md) | `mpv.conf` 的三档优先级、托管块所有权、让路规则、配置目录唯一派生点 | 改配置写入或设置持久化的人 |
| [`deploy-layout.md`](deploy-layout.md) | 配置层如何部署、构建产物在哪、哪些文件不能被覆盖 | 改部署或打包的人 |
| [`localization.md`](localization.md) | `AppLang` 构成、9 个语言文件的缺口、菜单与 `menus.json` | 改文案或菜单的人 |
| [`motion.md`](motion.md) | 三条曲线、三个时长、Translation 通道、减弱动画闸门 | 加或改动效的人 |
| [`verification.md`](verification.md) | 28 个检查器、两个测试套件、构建链路、**验证盲区** | 任何人提交前 |
| [`upstream-gap.md`](upstream-gap.md) | 与上游的许可边界、15 条提交的逐条处置、结构性差异 | 想「上游有这个我们为什么没有」的人 |
| [`history-filtering-log.md`](history-filtering-log.md) | 历史筛选工作的记录，只增不改 | 查历史决策的人 |

## 改 X 之前该读 Y

| 要改的东西 | 先读 | 理由 |
|---|---|---|
| `Domains/Settings/**` 下任何选项行 | `settings-contract.md` | 命名、分组与例外登记都在那里 |
| `MpvSettings.cs` 的 `ToCommand` / `MpvOptionNames` | `settings-contract.md` → `mpv-conf.md` | 一个决定运行时下发，另一个决定落盘与让路 |
| `ManagedMpvConfig.cs` / `MpvConfOverrides.cs` | `mpv-conf.md` | 所有权按行判定；配置目录只有一个派生点 |
| `ConfigDeployer.cs` / `data/deploy-config.ps1` | `deploy-layout.md` | 部署目标与排除表是两份手工同步的清单 |
| `Lexicon/**` / `Languages/*.json` / `Menus/menus.json` | `localization.md` | 只有 `zh-CN` 是完整的 |
| `Infrastructure/Motion/**` 或任何动画 | `motion.md` | 时长/曲线/通道三者只有一种正确写法 |
| 往任一层加文件 | `architecture.md` | 先看该层禁止什么 |
| 提交前 | `verification.md` | 哪些检查器必须跑、哪些地方没有测试 |

## 维护规则与数字口径

- 文档与代码冲突时以**代码为准**，并把冲突记进对应文档。
- 新增数字一律附命令，缺命令就不写数字。
- 交叉引用只允许指向本目录真实存在的文件。`check-doc-references.py` 与
  `check-doc-ranges.py` 用 glob 扫本目录全部 `.md`，各自有一份显式排除名单。
- 行数按「数 `\n`，末尾无换行则 +1」统计。`Measure-Object -Line` 不计空行，会少报约
  10%；`-split` 得到的 `.Count` 会多 1。
- 文件数与行数统一只数 `.cs` / `.xaml`；`README.md` 等非代码文件不计入。
- 重算命令：文件数与行数见 [`architecture.md`](architecture.md) §7，依赖方向同节，
  检查器与测试见 [`verification.md`](verification.md) §6。
- `tools/` 下只有被跟踪的脚本可用。开发期的一次性工具（`report-doc-numbers.py` 等
  17 个）现在归在仓库内的 `archive/`（被 `.gitignore` 排除，不随仓库分发）；其中两个本来就已失效，
  原因见 `verification.md` §4.5。
- 本目录的文档必须经 `check-doc-freshness.py`：不得写日期基线、不得保留历史态表述、
  不得声称某个仍存在的文件已被删除。
- 文档里写出的 `tools/`、`docs/` 路径必须能在**干净检出**里找到，由
  `check-doc-tooling.py` 用 `git ls-files` 判定。本机存在但被 `.gitignore` 排除的
  路径不算存在：README 的读者 clone 之后没有它。
