# 从旧 Agent 工作区抢救出来的文档

这里不是本项目的现行文档。**除 `docs/alignment/` 外，本项目任何文档关于"功能是否可用"的说法都不采信**，本目录同样适用——它们是历史产物，可能描述的是已经不存在的代码。

放进来的唯一理由是：它们在旧工作区里**独有**，删除后无法从任何远端重新取得。项目代码本身不需要它们。

采集日期：2026-10-27。采集方式见 `docs/alignment/LEGACY-WORKSPACES.md`。

## 来源对照

| 子目录 | 原位置 | 内容 | 为什么留 |
|---|---|---|---|
| `ikas-mc-audit/` | `Documents\Codex\2026-08-07\ikas-mc-mpv-winui-player-winui\outputs\` | 发布前审计（40 KB）+ 发布清单（6 KB），2026-08-07 | 逐项核查了许可证、本地化、纯净度，附证据路径。本项目 `docs/` 里没有同内容文件 |
| `menu-host-handoff/` | `Documents\Codex\2026-08-07\mpv-winui-menu-host-handoff-20260807\outputs\` | 菜单宿主交接文档 + 补充，2026-08-07 | 记录了菜单宿主（独立进程托管右键菜单）的设计与验证过程 |
| `menu-host-analysis/` | 同上 `work\` 顶层 | 架构计划、兼容矩阵、依赖分析、DPI 检查日志、闪烁修复日志、评审报告 ×2、评审清单 ×3、探针日志、E2E 证据、Richasy/mpv-winui 参考研究、was-upgrade 调研，共 16 份 | 这批是本项目**早期技术决策的唯一记录**，尤其是 DPI 与菜单窗口层级，与 2026-10-27 的实测互相印证 |
| `playkit-upgrade/` | `Documents\Codex\2026-08-04\mpv\outputs\` | mpv-lazy 升级报告，2026-08-04 | 配置层从上游同步时的差异记录 |
| `playkit-5-review/` | `Documents\Codex\2026-08-06\1-mpv-lazy-hooke007-mpv-playkit-5\outputs\` | mpv 评审清单 | 与 `menu-host-analysis` 的清单互为前后版本 |
| `winui-polish-0808/` | `Documents\Codex\2026-08-08\mpv-winui-player-2026-08-08-4\outputs\` | 界面打磨记录，2026-08-08 | 当时的问题清单与处理 |

## 需要特别说明的一份

`menu-host-analysis/dpi-check-log.txt` 是 2026-08-07 菜单宿主的运行日志，里面

```
WINDOW size=300x81 pos=2200,600 scale=1 workW=1920 (pass1)
```

`scale=1` 与 `workW=1920` 说明当时那台会话是 100% 缩放，而本机现在是 175%。**不要把这份日志里的像素数值当作本机现状**。它的价值在于记录了"两遍布局 pass1/pass2"和"prewarm 子进程"这两个机制，后者在 `AGENTS.md` 的菜单宿主条目里有对应描述。

## 没有放进来的东西

同一批工作区里还有大量内容被**排除**，理由记录在 `docs/alignment/LEGACY-WORKSPACES.md` 的逐目录判定表里。简要：

- 约 3.4 GB 的 `ikas-mc/mpv-winui-player` 克隆 —— HEAD `47f30f4` 已在 `main` 里
- CUDA / TensorRT 运行时 DLL、`.7z` 分卷、onnx 模型测试集 —— 可重新下载或重新构建
- 全部截图、`.log`、`.err`、`.ps1` 探针 —— 一次性产物
- 5 个只有空 `outputs/` `work/` 的壳目录

## 处置建议

这些文档可以随时删除，不阻塞任何事。若确认不再需要，删除本目录即可；代码与构建都不引用它。
