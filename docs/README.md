# docs 索引

本目录是 mpvw 的项目契约。每份文档只描述**当前代码里的事实**，不写计划、不写日期基线。
数字一律附重算命令；读不出原因的约束会标注「原因不明」。

## 文档职责

| 文件 | 规定什么 | 读者 |
|---|---|---|
| [`architecture.md`](architecture.md) | 四层与 8 个域的职责边界、禁止事项、实测依赖方向 | 准备跨目录改代码的人 |
| [`settings-contract.md`](settings-contract.md) | 设置界面：三层分工、命名与分组、验证流程、已知例外登记 | 改 `Domains/Settings`、`SettingsModel`、`MpvConf` 的人 |
| [`mpv-conf.md`](mpv-conf.md) | `mpv.conf` 的三档优先级、托管块所有权、让路规则、**运行期路径冲突** | 改配置写入或设置持久化的人 |
| [`deploy-layout.md`](deploy-layout.md) | 配置层如何部署、构建产物在哪、哪些文件不能被覆盖 | 改部署或打包的人 |
| [`localization.md`](localization.md) | `AppLang` 构成、9 个语言文件的缺口、菜单与 `menus.json` | 改文案或菜单的人 |
| [`motion.md`](motion.md) | 三条曲线、三个时长、Translation 通道、减弱动画闸门、已踩的坑 | 加或改动效的人 |
| [`verification.md`](verification.md) | 25 个检查器、两个测试套件、构建链路、**验证盲区** | 任何人提交前 |
| [`upstream-gap.md`](upstream-gap.md) | 与上游的差异及为何不移植 | 想「上游有这个我们为什么没有」的人 |
| [`history-filtering-log.md`](history-filtering-log.md) | 历史筛选工作的记录，只增不改 | 查历史决策的人 |
| [`compare-upstream.md`](compare-upstream.md) | 与上游的逐项对比清单 | 做上游对齐评估的人 |

## 改 X 之前该读 Y

| 要改的东西 | 先读 | 理由 |
|---|---|---|
| `Domains/Settings/**` 下任何选项行 | `settings-contract.md` | 命名、分组与例外登记都在那里 |
| `MpvSettings.cs` 的 `ToCommand` / `MpvOptionNames` | `settings-contract.md` → `mpv-conf.md` | 一个决定运行时下发，另一个决定落盘与让路 |
| `ManagedMpvConfig.cs` / `MpvConfOverrides.cs` | `mpv-conf.md` | 所有权按行判定；**先看 §1 的路径冲突** |
| `ConfigDeployer.cs` / `data/deploy-config.ps1` | `deploy-layout.md` | 部署目标与排除表是两份手工同步的清单 |
| `Lexicon/**` / `Languages/*.json` / `Menus/menus.json` | `localization.md` | 只有 `zh-CN` 是完整的 |
| `Infrastructure/Motion/**` 或任何动画 | `motion.md` | 时长/曲线/通道三者只有一种正确写法 |
| 往任一层加文件 | `architecture.md` | 先看该层禁止什么 |
| 提交前 | `verification.md` | 哪些检查器必须跑、哪些地方没有测试 |

## 维护规则与数字口径

- 文档与代码冲突时以**代码为准**，并把冲突记进对应文档。
- 新增数字一律附命令，缺命令就不写数字。
- 交叉引用只允许指向本目录真实存在的文件。**没有自动校验**：`check-doc-references.py`
  只读它 `DOCS` 列表里的旧文档（那些文件已删除，见下）。
- 行数按「数 `\n`，末尾无换行则 +1」统计。`Measure-Object -Line` 不计空行，会少报约
  10%；`-split` 得到的 `.Count` 会多 1。
- 重算命令：文件数与行数见 [`architecture.md`](architecture.md) §7，检查器与测试见
  [`verification.md`](verification.md) §6。
- `tools/report-doc-numbers.py` 未入库且已失效（指向旧路径，抛 `FileNotFoundError`），不要依赖它。

## 旧文档已删除，引用已全部重定向

本套文档替换了 5 份旧文档（settings-design-contract.md、motion-system.md、
mpv-conf-precedence.md、deploy-layout.md、localization.md）。所有活跃引用
（代码注释、工具、检查器的 DOCS 列表）都已改为指向新文档，已实测
git grep 'settings-design-contract\|mpv-conf-precedence\|motion-system' 在 .md 之外无命中。

	ools/check-doc-ranges.py 与 	ools/check-doc-references.py 的 DOCS 列表也已改为
新文档，所以它们现在真的在校验这 6 份文档（实测 105 条引用、37 个行号范围，0 失效）。