# UI 修复任务：长期、持续、不中断

这是一份**给新对话直接执行**的任务书。读完这一页就能开工，不需要先读别的文档。
配套工件：

| 文件 | 作用 |
|---|---|
| `docs/ui-repair/QUEUE.md` | 缺陷台账。**唯一的进度依据**，每轮从这里取一项 |
| `docs/ui-repair/PLAYBOOK.md` | 每一轮的固定动作：怎么定位、怎么修、怎么验证、怎么记录 |
| `docs/ui-repair/BASELINE.md` | 官方规范、上游实现、本地令牌的引用口径。判断"什么是错的"靠它 |
| `mpv-winui/mpv-winui/DOMAINS.md` + 14 个分区 README | 分区域上下文入口。按域加载，不通读全项目 |
| `tools/ui-repair/` | 本轮任务的工具链：`next.py` `repro.py` `gate.py` `done.py` `scan.py` `scan-skins.py` `scan-skins-pixels.py` `compare-upstream.py` |
| `tools/mvpw-mcp/` | 可编程驱动应用的 MCP 工具与验证器 |

---

## 1. 任务是什么

把 `mpvw` 的功能与排版缺陷持续修到"实测通过"。不是写报告，不是列清单，是让应用在
UI Automation 和 IPC 的测量下真的正确。

**循环**：审查 → 定位 → 修复 → 验证 → 记录 → 下一项。修完一项立刻取下一项。

## 2. 自主权边界

**直接做，不要问**：

- 官方 WinUI 3 / Fluent 规范怎么写的、`winapp find-api` 说 API 是什么形状
- 上游 `ikas-mc/mpv-winui-player` 怎么实现的 → 有则对齐，无则按规范实现
- 本地令牌（`MpvSpace*` / `MpvFont*` / `MotionTokens`）该怎么用
- 哪个分区的代码该删、该并、该改名
- 布局参数、间距、字号、动画曲线、时长
- 测试怎么加、验证器怎么写

**只在两种情况下停下**：

1. 需要**破坏性外部动作**：改远程分支历史、发布、删用户数据
2. 两个方案**证据对等**且影响面大，需要产品判断（例如"这个功能要不要保留"）

其余一律按最优判断推进，并在 `QUEUE.md` 记录决策与依据。

## 3. 三条硬规则

### 3.1 先证明缺陷存在，再修

这个仓库的历史里，**每一次**"看起来是 bug"都有一半概率是探针自己的问题。已记录的误判：

| 声称的缺陷 | 实测真相 |
|---|---|
| 导航栏在播放时消失 | 设计如此，`ShellBackButton` 恢复 |
| 左边缘有竖排文字 | 窗口投影，像素 `(218,219,220)` |
| 排序按钮溢出 | `delta=-56`，没溢出 |
| 控制面板旁有黑色块 | 视频内容，19%/44% 饱和度 |
| 34 个分类 | `CategoryNav` 是整个 NavigationView，pane 内恰好 6 个 |
| 菜单为空是缺陷 | 无媒体时 `menu-data` 是 `[]`，`loadfile` 后 24 项 |
| 三个导航项坏了 | 探针自己的 `back_to_library` 在健康窗口上按了返回键 |

**判据**：能用两条独立路径复现，才算缺陷。写进台账时附上复现命令与原始输出。

### 3.2 不用固定 `sleep`，等元素

固定时长是间歇性失败的头号来源，本仓库反复踩：

```
verify_e2e      time.sleep(1.2)  → 14/19, 14/19, 19/19   （猜时长）
                改 wait_for()    → 19/19, 19/19, 19/19   （等元素）
launch_app      time.sleep(1.1)  → 0/3 拿到快照
                改轮询           → 4/4
```

写验证器时：等到**要断言的元素**出现，超时才算失败。

### 3.3 有据可依，禁止手搓

排版、颜色、间距、字号、动画、控件选择，必须指向下列之一：

- WinUI 3 / Fluent 官方规范与主题资源（`ThemeResource`、`*TextBlockStyle`）
- 上游 `ikas-mc` 的同类实现
- 本地已定义的令牌（`Styles.xaml` 的 `Mpv*` 键、`MotionTokens.cs`）

三者都没有，就先在 `BASELINE.md` 补一条依据，再动手。**不接受"看起来差不多"**。

## 4. 每一轮怎么做

完整版在 `PLAYBOOK.md`。最短路径：

```bash
# 0. 从台账取一项，看它的分区入口
python tools/ui-repair/next.py            # 打印当前该做的那一项

# 1. 复现（两条路径）
python tools/ui-repair/repro.py <id>      # 打印该缺陷的复现命令并执行

# 2. 修

# 3. 验证（必须全绿才算修好）
python tools/ui-repair/gate.py            # 构建 + 检查器 + 验证器 + 部署一致性

# 4. 记录
python tools/ui-repair/done.py <id> --evidence "..."
```

## 5. 什么算"修好了"

`gate.py` 全绿，**且**该缺陷的复现脚本从"失败"翻转为"通过"，**且**这一翻转在两次
独立运行中都成立。三者缺一不算完成，台账不得标记 done。

## 6. 上下文怎么控制

- **一次只读一个分区**：`Modules/<Domain>/README.md` 是该域的入口（职责、公开接口、
  关键状态、依赖方向、验证命令）
- **不要通读** `Player`（79 文件）和 `Settings`（87 文件）；它们各自有 README 指向
  具体 partial 的边界
- 需要跨域时，先读 `mpv-winui/mpv-winui/DOMAINS.md` 的依赖方向表
- 文档口径冲突时，以 `docs/settings-design-contract.md` 为准（设置区），
  其余以 `BASELINE.md` 为准

## 7. 工具速查

```bash
# 驱动应用（窗口会停到屏幕外，不抢焦点）
python tools/mvpw-mcp/verify_e2e.py            # 19 步冒烟
python tools/mvpw-mcp/verify_settings.py
python tools/mvpw-mcp/verify_settings_rows.py
python tools/mvpw-mcp/verify_screenshot.py
python tools/mvpw-mcp/verify_no_focus_steal.py

# 构建与部署
pwsh build.ps1 -Configuration Release -Platform x64
python tools/check-xaml.py                     # 每次改 XAML 前先跑
pwsh tools/deploy-test.ps1 -CloseRunning -Verify
```

**不要用 `tools/mvpw-mcp/audit_domains.py` 的摘要作为证据** —— 它在一轮会话里四次
把探针问题归因给产品，文件头已写明这一点。

## 8. 夜间自动扫描

每天 03:00 有一个定时任务跑 `python tools/ui-repair/nightly.py`，产出
`_work/nightly/report.md`。报告把发现分成两类：

| 类别 | 含义 | 谁处理 |
|---|---|---|
| **已能定的** | 结论明确、不需要产品判断（如"应用现在不写日志"） | 定时任务自己修，走 PLAYBOOK 完整循环 |
| **需要你决策的** | 结论不唯一，或需要接受某个取舍 | 早上留给用户 |

它**不是**自动改码机器人。全自动改代码只允许「不改变布局、且有等价证明」的替换，
目前实际可自动修的类别是 0 —— 这个结论来自三次失败的尝试：

| 尝试 | 结果 |
|---|---|
| 字面字号 → 令牌 | 81 个按值匹配 → 0 个真缺陷。全是图标字号，或令牌语义不符 |
| 死类型 | 22 个候选 → 全误报。`<ctl:Name>`、`x:DataType`、`{x:Bind local:X.Y()}` 正则看不清 |
| 孤儿资源键 | 0（本来已干净） |

所以夜扫描的真正价值是**反复测量**，不是自动修改。它擅长的是：跑一遍 110 秒的
`gate.py`、抓 `display-info.log`、比对上游文件树、扫描无障碍与皮肤像素——这些
昂贵但不需要判断的动作。

## 9. 起手第一件事

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv
python tools/ui-repair/next.py
```

它会打印当前该修的项、该读的分区入口、以及复现命令。然后按 `PLAYBOOK.md` 走。
