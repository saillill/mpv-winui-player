# 分区 4 · 菜单 / Menu

## 三条互相独立的菜单路径（先分清，别混改）

| 路径 | 入口 | 数据来源 | 渲染 |
|---|---|---|---|
| **A. 菜单栏**（窗口顶部） | `MenuBar/MenuBarService.cs`、`Player/Menu/MenuBarBuilder.cs`、`MpvPlayerPage_MenuBar.cs` | `Menus/menus.json` | `MenuBar` 控件 |
| **B. 右键菜单** | `Player/MpvPlayerPage_MpvMenu.cs` | mpv 的 `menu-data`（由 `dyn_menu.lua` 从 `input.conf` 的 `#menu:` 注解生成） | `MenuFlyout`，纯文本 |
| **C. 菜单编辑器** | `MenuEditor/MenuEditorWindow.xaml(.cs)` + 5 个 partial | `Menus/menus.json` | 编辑后写 `menu.conf` |

三条路径**互不影响**。历史上把 A 和 B 当成一件事改，结果两处都错。

## 职责

- 菜单栏的构建与本地化（A）
- 右键菜单从 mpv `menu-data` 到 `Flyout` 的转换（B）
- `menu.conf` 的解析 / 写入 / 树形编辑（C）

**不负责**：`input.conf` 的键位本身（在 `data/input.conf`，由 mpv 读）。

## 入口文件

| 文件 | 作用 |
|---|---|
| `MenuBar/MenuBarService.cs` | 菜单栏服务 |
| `MpvMenu/MenuConfParser.cs` / `MenuConfWriter.cs` | `menu.conf` 读写（**本项目唯一实现**） |
| `MpvMenu/MpvMenuItem.cs` | 菜单项模型 |
| `MenuEditor/MenuEditorPage*.cs` | 编辑器 5 个 partial（TreeView / Drag / Preview / Conf / 主） |
| `MenuEditor/MenuType.cs`、`MenuTreeItem.cs` | 编辑器模型 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MenuBarService` | 构建菜单栏 | `MpvPlayerPage_MenuBar.cs` |
| `MenuConfWriter` | `Write(items, path)` | 菜单编辑器 |
| `MenuConfParser` | `Parse(text)` | 菜单编辑器、测试 |

## 依赖

- 依赖：`MpvConf/Conf`（读写 conf 的通用能力）、`Language`、`Infrastructure/Common`。
- **不依赖**：`Player`（`MpvPlayerPage_MpvMenu.cs` 反过来依赖本区的模型，方向正确）。

## 关键约定

- **右键菜单是纯文本，没有图标、没有过滤、没有重命名**。要隐藏条目就改 `input.conf`，
  `HiddenMenuTitles` 已被移除。
- **菜单项顺序 = `input.conf` 注解顺序**（与 mpv-menu-plugin 一致），不要在 UI 层重排。
- **`dyn_menu.lua` 是 GPL-2.0-only 的上游文件 + 有记录的本地补丁**，不是本项目代码。
  改它要走 `data/scripts/LOCAL-PATCHES.md` 的补丁流程，保持 GPL 边界。
- **`dynamic_menu.lua`（6.6 KB）与 `dyn_menu.lua`（66 KB）不是重复**：
  前者生成 VSR/HDR 动态子菜单，通过 `menu-ready` 脚本消息被后者通知刷新。
  合并它们会破坏这个契约。

## 上下文裁剪提示

只改右键菜单 → 读 `MpvPlayerPage_MpvMenu.cs`（10 KB）+ `input.conf` 的 `#menu:` 行。
只改菜单编辑器 → 只碰 `MenuEditor/` 目录，它和播放路径没有共享状态。
