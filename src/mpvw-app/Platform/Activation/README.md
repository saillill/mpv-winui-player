# 分区 8 · 激活 / Activation

## 职责

让 mpvw 被系统"叫起来"：双击媒体文件、右键"打开方式"、`mpvw://` 协议链接、
以及把文件关联登记到注册表。

## 入口文件

| 文件 | 作用 |
|---|---|
| `ActivationService.cs` | 解析启动参数（文件 / URL / 协议），决定怎么开 |
| `ActivationRegistrationService.cs` | 写 / 删注册表项，登记文件类型与协议 |
| `MpvProtocolUri.cs` | `mpvw://` URI 的解析与校验（纯函数，有 8 个单元测试） |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `ActivationService` | 处理启动激活 | `App.xaml.cs` |
| `ActivationRegistrationService` | `Register(kind)` / `Unregister(kind)` | 设置页 `SectionProgramAssociations` |
| `MpvProtocolUri` | `TryParse(uri, out ...)` | `ActivationService`、测试 |

## 依赖

- 依赖：`Common/Utils`、`Language`。
- **不依赖 `Player`**：激活只解析参数，开文件由 `Shell/MainWindow.Open` 负责。

## 边界

文件关联的 UI 在设置页，不在这一层。这一层只有解析与注册：`ActivationService`、
`ActivationRegistrationService`、`MpvProtocolUri`。**不要在这里加 XAML 控件或 Item 模型**
——UI 归 `Domains/Settings`。

## 上下文裁剪提示

改协议解析 → 只读 `MpvProtocolUri.cs`（4.6 KB），跑 `MpvProtocolUriTests`。
改文件关联 → `ActivationRegistrationService.cs` + 设置页的
`SettingsPage.Options.Program.cs` 中 `SectionProgramAssociations` 段。
