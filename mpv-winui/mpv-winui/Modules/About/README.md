# 分区 11 · 关于 / About

## 职责

「关于」对话框：版本、许可证、第三方组件、项目链接。

## 入口文件

| 文件 | 作用 |
|---|---|
| （无本地文件）| 对话框由 `Modules/Player/MpvPlayerPage_MenuBar.cs:585` 的 `ShowAboutDialogAsync()` 现场搭建 |

> **本目录曾经有一个 `AboutControl.xaml(.cs)`**，类文档说它由设置页的关于
> section 宿主。实测：`AboutControl.Create()` 在整仓零调用，`git grep
> AboutControl` 只命中该文件自身，设置页与菜单都没有引用它。真正的对话框一直是
> `ShowAboutDialogAsync()` 里手搭的 `StackPanel`。该控件已于 2026-10-22 删除
> （`cleanup: what the inventory proved dead`），本目录因此不再有源文件。

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MpvPlayerPage` | `ShowAboutDialogAsync()` | 菜单 `about` 动作（`MpvPlayerPage_MenuBar.cs:91` 注册） |

## 依赖

- 对话框本身只依赖 `AppContext.AppLang` 与 `Common/Utils/UpdateChecker`。
- **不依赖其他功能域。**

## 上下文裁剪提示

读 `MpvPlayerPage_MenuBar.cs:585-630` 即可（约 45 行）。许可证文本的真相源是
`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`，对话框只是复述——
**改许可证时两处都要改**。

