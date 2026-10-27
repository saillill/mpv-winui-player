# 分区 9 · 平台 / Mpv（Win32）

## 职责

绕不开 Win32 的部分：全局键盘钩子、键码映射、窗口样式常量。

> 目录名 `Mpv` 有历史原因（最初只放 mpv 相关的 Win32 粘贴），实际内容是
> **通用 Win32 互操作**。分区方案里建议改名为 `Platform/Win32`，见
> `docs/domain-partition-plan-20261021.md` 阶段 B。

## 入口文件

| 文件 | 作用 |
|---|---|
| `W32Keyboard.cs` | 低级键盘钩子（全局快捷键） |
| `Keycodes.cs` | 键名 ↔ 虚拟键码（14.7 KB 的表） |
| `W32Common.cs` | 通用 P/Invoke 与常量 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `W32Keyboard` | 钩子的安装/卸载、按键事件 | `MpvPlayerPage` 的输入处理 |
| `Keycodes` | `Parse(name)` / `ToName(vk)` | 快捷键设置行、`input.conf` 校验 |

## 依赖

- 只依赖 .NET。**不依赖任何 `Domains/*`、`Shell`、`Language`。**

## 关键约定

- **`Keycodes` 是纯数据 + 纯函数**，没有 UI 依赖；`mpv-conf-test` 里有用例依赖这一性质，
  不要往它里面塞 `AppContext`。
- **全局钩子必须能被卸载**：它挂在进程级，不卸载会导致退出后键盘仍被拦截。

## 上下文裁剪提示

加一个键 → 只读 `Keycodes.cs` 的表结构 + `input.conf` 的对应行，
跑 `tools/check-conf-lines.py`（它校验键位与选项名）。
