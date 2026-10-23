这是一个长期、不中断的 UI 修复任务。以下是你需要遵循的全部约定。

## 任务

把 `mpvw`（本仓库，WinUI 3 + 内嵌 libmpv）的功能与排版缺陷持续修到"实测通过"。

循环：**审查 → 定位 → 修复 → 验证 → 记录 → 下一项**。修完一项立刻取下一项，
不要每修一项就停下来等待确认，不要问"要不要继续"。

## 起手第一步

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv
python tools/ui-repair/next.py
```

它会告诉你要修哪一项、先读哪个分区 README、用什么命令复现。然后按
`docs/ui-repair/PLAYBOOK.md` 走完这一轮。

## 自主权

**直接做，不要问**：官方 WinUI 3 / Fluent 规范怎么写的、`winapp find-api` 说 API
是什么形状、上游 `ikas-mc/mpv-winui-player` 怎么实现的、本地令牌怎么用、哪个分区的
代码该删该并该改名、布局参数与间距字号、动画曲线与时长、测试与验证器怎么写。

**只在两种情况下停**：
1. 需要破坏性外部动作（改远程历史、发布、删用户数据）
2. 两个方案证据对等且影响面大，需要产品判断

其余一律按最优判断推进，并在 `docs/ui-repair/QUEUE.md` 的决策记录里写下依据。
不要怕大改，不要怕风险。

## 三条硬规则

**1. 先证明缺陷存在，再动产品代码。**

这个仓库的探针误判率高于真缺陷率。已记录的误判：导航栏在播放时"消失"（设计如此）、
左边缘"竖排文字"（窗口投影）、排序按钮"溢出"（delta=-56）、菜单"为空"（没加载媒体）、
三个导航项"坏了"（探针自己按了返回键把导航栏去掉了）。

判据：两条独立测量都成立才算缺陷。复现没写出来之前，不许改产品。

**2. 不用固定 sleep，等元素。**

固定时长是本仓库间歇性失败的头号来源。实测：`time.sleep(1.2)` 得 14/19、14/19、
19/19；改成 `wait_for(元素)` 得 19/19 × 3。写验证器时等到要断言的元素出现，超时才算
失败。

**3. 有据可依，禁止手搓。**

排版、颜色、间距、字号、动画、控件选择，必须指向：官方规范与主题资源、上游
`ikas-mc` 的同类实现、或本地已定义的令牌（`Styles.xaml` 的 `Mpv*`、`MotionTokens.cs`）。
三者都没有就先在 `docs/ui-repair/BASELINE.md` 补一条依据再动手。不接受"看起来差不多"。

## 什么算修好了

`python tools/ui-repair/gate.py` 全绿（8 阶段：check-xaml、构建、19 个检查器、部署
一致性、e2e 19/19、settings、settings_rows、screenshot），**且**该项的复现从失败
翻转为通过，**且**这个翻转在两次独立运行中都成立。

然后 `python tools/ui-repair/done.py <id> --evidence "..."`。它会在复现仍失败时拒绝。
一个缺陷一个提交，提交信息写清根因，不要写"修复若干问题"。

## 上下文控制

- 一次只读一个分区：`mpv-winui/mpv-winui/Modules/<Domain>/README.md` 是该域入口
- 不要通读 `Player`（79 文件）和 `Settings`（87 文件），各自有 README 指向 partial 边界
- 需要跨域时先读 `mpv-winui/mpv-winui/DOMAINS.md` 的依赖方向表
- 文档冲突：设置区以 `docs/settings-design-contract.md` 为准，其余以
  `docs/ui-repair/BASELINE.md` 为准

## 必要文件

| 文件 | 作用 |
|---|---|
| `docs/ui-repair/README.md` | 任务书全文 |
| `docs/ui-repair/PLAYBOOK.md` | 每轮的具体步骤，含卡住时怎么办 |
| `docs/ui-repair/BASELINE.md` | 判断"什么是错的"的依据 |
| `docs/ui-repair/QUEUE.md` | 缺陷台账，唯一进度依据 |

不要用 `tools/mvpw-mcp/audit_domains.py` 的摘要作为证据——它在一轮会话里四次把探针
问题归因给产品，文件头已写明这一点。用 `verify_*.py`。

## 驱动应用的方式

窗口会自动停到屏幕外，不会抢你的焦点。可用的验证器：

```bash
python tools/mvpw-mcp/verify_e2e.py            # 19 步
python tools/mvpw-mcp/verify_settings.py
python tools/mvpw-mcp/verify_settings_rows.py
python tools/mvpw-mcp/verify_screenshot.py
python tools/mvpw-mcp/verify_no_focus_steal.py
```

IPC 硬约束：一个进程只在启动后约 1 秒内接受一个连接。需要 IPC 的探针先
`open_launch_window()` 再立刻发请求，不要 sleep。这不是产品缺陷，是嵌入方式。

## 现在开始

跑 `python tools/ui-repair/next.py`，然后按 PLAYBOOK 走。不要先汇报计划，直接做。
