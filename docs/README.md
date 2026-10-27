# docs 索引

本目录的每份文档属于下列四类之一。**类别决定它的生命周期与改动权限**——
改契约要先确认，改历史记录随便改，审计报告只读，归档不进索引。

写于 2026-10-27，随 `06-DOCS-CLEANUP.md` 的清理结果更新。

---

## 分类规则

| 类别 | 定义 | 判据 | 生命周期 |
|---|---|---|---|
| **契约 Contract** | 描述"必须遵守的规则"。代码注释可以引用它的行号 | 有代码或检查器引用 | 长期；改动需评审 |
| **参考 Reference** | 描述"当前是什么样"的事实 | 无强制约束力，但被多处引用 | 随代码演进更新 |
| **记录 Record** | 某次工作过程的结论与证据 | 带日期或会话标记 | 只增不改；过时后归档 |
| **审计 Audit** | 一次性调查的完整报告 | 带日期，基线提交已远离 HEAD | 只读；不维护 |

**一条硬规则**：任何文档不能同时是"契约"和"审计"。如果一份文档既规定规则又记录调查，
必须拆成两份。

---

## 契约（5 份，改动需评审）

| 文件 | 规定什么 | 谁在引用 |
|---|---|---|
| [`settings-design-contract.md`](settings-design-contract.md) | 设置界面的结构、命名、分组、交互、验证流程，含 33 条已知例外登记 | **14 处**：`AGENTS.md`、`MpvConfManager.cs`、`OptionListControl.xaml`、`check-localization.py`、`docs/alignment/ISSUE_LOG.md` 等 |
| [`deploy-layout.md`](deploy-layout.md) | 构建输出与部署目录的布局，哪些能挪、哪些不能 | 3 处：`AGENTS.md`、`check-doc-ranges.py`、`check-doc-references.py` |
| [`mpv-conf-precedence.md`](mpv-conf-precedence.md) | mpv.conf 的优先级模型 | 4 处：**3 个 `.cs` 文件的注释**（`ManagedMpvConfig.cs`、`SettingsPage.Actions.cs`、`SettingsPage.Options.Program.cs`） |
| [`localization.md`](localization.md) | 本地化与菜单栏/input.conf 约定 | 2 处：`AGENTS.md`、`check-doc-references.py` |
| [`state-ownership.md`](state-ownership.md) | 状态归属表：哪份状态由谁拥有 | 0 处外部引用——**待升格**，见下方"待办" |

**`settings-design-contract.md` 是唯一被代码注释引用行号的文档。**
删改它会直接让 `check-doc-ranges.py` 与 `check-doc-references.py` 报红。

---

## 参考（4 份，随代码更新）

| 文件 | 内容 | 注意 |
|---|---|---|
| [`settings-ui-review.md`](settings-ui-review.md) | 设置界面分区如何选出、调研过程与改造计划 | 名字像审计，实际被 `SettingsSections.cs` 注释引用，因此归类为参考 |
| [`motion-system.md`](motion-system.md) | WinUI 3 动效体系：token、easing、spring、gate、behaviors | 被 `docs/ui-repair/BASELINE.md` 引用 |
| [`missing-options.md`](missing-options.md) | 设置界面尚未暴露的 mpv 选项 | **由 `tools/report-missing-options.py` 生成**，不要手改 |
| [`compare-upstream.md`](compare-upstream.md) | `upstream-merge` 与上游的差异清单 | 在 `check-doc-references.py` 白名单内，不能删 |

---

## 记录（2 份）

| 文件 | 内容 |
|---|---|
| [`project-overview-20261021.md`](project-overview-20261021.md) | 项目总览与对齐报告。**其术语表是 `DOMAINS.md` 指定的"唯一口径"**，但基线是 2026-10-21 |
| [`domain-partition-plan-20261021.md`](domain-partition-plan-20261021.md) | 12 域划分草案。**已被 `prompt-v2/05-MODULE-DESIGN.md` 取代**，保留作历史 |

---

## 审计（1 份，只读）

| 文件 | 内容 |
|---|---|
| [`audit-and-roadmap-20260828.md`](audit-and-roadmap-20260828.md) | 2026-08-28 的系统审计与"长期路线图"。路线图已被 `prompt-v2/02-TASK.md` 的七项要求取代；保留因为 `data/docs/README.md` 引用它 |

---

## 子目录

| 目录 | 内容 | 类别 |
|---|---|---|
| [`alignment/`](alignment/) | 上一轮会话（2026-10-26/27）的产出：基线、术语、历史索引、环境、工具盘点、验证结果 | **记录**——不是当前契约。其中 `VERIFICATION-2026-10-27.md` 有一处已被证伪（见 `prompt-v2/03-REVIEW-20261027.md`） |
| [`ui-repair/`](ui-repair/) | 长周期 UI 修复任务的作业区：队列、基线、诊断、状态 | **记录 + 作业数据**。`QUEUE.md` 是 `tools/ui-repair/` 的数据源，被 5 个脚本读取 |
| [`legacy-agent-workspaces/`](legacy-agent-workspaces/) | 若干旧 AI 工作会话归档（22 md / 78 文件 / 3.7 MB），含可执行脚本 | **归档**。**待移出**，见 `prompt-v2/06-DOCS-CLEANUP.md` §6 |

---

## 2026-10-27 已删除的 12 份文档

这些曾是"审计"类，实测**零外部引用**且内容已被取代，已删除。清单与理由见
[`prompt-v2/06-DOCS-CLEANUP.md`](prompt-v2/06-DOCS-CLEANUP.md)。

```
component-review.md                      audit-charter.md
delivery-20261022.md                     audit-cleanup-list.md
winrt-surface.md                         cloud-diff-audit-20261021.md
fontsize-classification.md               interference-inventory-20261021.md
spacing-classification.md                skin-scp-research-20261021.md
handoff-20261012.md                      wmp-study-and-handoff.md
```

删除后 `check-doc-references.py`（broken: 0）与 `check-doc-ranges.py`（stale: 0）仍为绿。

---

## 待办

1. **`state-ownership.md` 尚未验证与当前代码一致。** 它零外部引用，但状态归属有长期价值。
   需要逐条核对后再决定升格为契约还是归档。
2. **`project-overview-20261021.md` 需要拆分**：术语表进契约（与 `docs/alignment/TERMINOLOGY.md`
   合并），其余归档。现在它身兼"术语唯一口径"与"过时的总览"两职。
3. **`legacy-agent-workspaces/` 待移出**：它使"旧路径引用为 0"无法达成
   （内含十几条 `C:\Users\z2637\Documents\Codex\...` 历史证据）。
4. **`alignment/HISTORY.md` 在要求 3 重建历史后失效**（提交号全变）。
   届时归档，不删除——它是筛选工作的输入。

---

## 怎么用这份索引

- **要改 `Modules/Settings/` 下的东西**：先读 `settings-design-contract.md`。
- **要改 mpv.conf 的读写**：先读 `mpv-conf-precedence.md`。
- **要改菜单或 input.conf**：先读 `localization.md`。
- **要改构建或部署路径**：先读 `deploy-layout.md`。
- **要理解为什么目录是这样**：读 `prompt-v2/05-MODULE-DESIGN.md`。
- **想知道某个模块能做什么**：读模块自己的 `README.md`（14 个分区各有 README，
  入口是 `mpv-winui/mpv-winui/DOMAINS.md`）。
