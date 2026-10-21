# 分区 11 · 关于 / About

## 职责

「关于」页：版本、许可证、第三方组件、项目链接。

## 入口文件

| 文件 | 作用 |
|---|---|
| `AboutControl.xaml(.cs)` | 控件（不是窗口，宿主由调用方提供） |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `AboutControl` | 无参构造 | 设置页的关于 section、`MainWindow` 的关于入口 |

## 依赖

- 依赖：`Common`（`BaseWindow` 之外的控件基类）、`Language`、`Common/Utils/UpdateChecker`。
- **不依赖其他功能域。**

## 上下文裁剪提示

3 KB 的两个文件，直接读完。许可证文本的真相源是
`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`，About 页只是复述——
**改许可证时两处都要改**。
