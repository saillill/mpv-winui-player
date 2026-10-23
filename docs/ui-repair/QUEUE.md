# QUEUE：缺陷台账

**这是唯一的进度依据。** 每轮从这里取一项，修完标 `done` 并写证据。

状态取值：`open` / `in-progress` / `done` / `blocked` / `cannot-reproduce` / `probe-bug`

---

## 怎么读这一页

| 字段 | 含义 |
|---|---|
| `id` | 稳定标识，`next.py` / `repro.py` / `done.py` 用它定位 |
| `domain` | 分区，决定先读哪个 README |
| `kind` | `layout` 排版 / `behavior` 功能 / `a11y` 无障碍 / `port` 移植 / `cleanup` 清理 |
| `repro` | `confirmed` 有已证复现 / `unverified` 是假设 |
| `evidence` | 复现命令或测量值。**没有证据的项不得标 confirmed** |

---

## 当前基线（2026-10-24 实测）

| 项 | 结果 |
|---|---|
| `verify_e2e` | 19/19 |
| `verify_settings` | 0 failure |
| `verify_settings_rows` | PASS |
| `verify_screenshot` | PASS |
| `verify_no_focus_steal` | 3/3 |
| 17 个 `tools/check-*.py` | 0 失败 |
| 构建 | 0 错误 |
| 部署 | `identical: 658 / differing: 0` |
| WUI 分析器 | 24 条，**全部已证误报** |

---

## A. 可立即开工

| id | domain | kind | repro | 症状 | evidence |
|---|---|---|---|---|---|
| `upstream-fileassoc` | Activation | port | confirmed | 无任何文件/协议关联 UI；上游有 `ActivationRegistration/`（5 文件） | `repro.py` 输出：association UI 0，registry writers 0 → FAIL |
| `upstream-displayrequest` | Player | behavior | confirmed | 播放时不阻止息屏；上游有 `SimpleDisplayRequest`/`SystemDisplayRequest` | `repro.py`：display-wake mechanisms 0 → FAIL |

> 这两项都写好了 repro，当前 FAIL。修完应翻转为 PASS，**且连跑两次**。

## B. 已验证存在，不必移植（本会话结论）

| 声称缺失 | 实测 | 结论 |
|---|---|---|
| `upstream-appconfdata` | `ImportConfigAsync`/`ExportConfigAsync` 在 `SettingsPage.Actions.ImportExport.cs`，同文件还有 picker | **已存在**，repro PASS |
| `upstream-win32cmdline` | `App.xaml.cs` + `ActivationService.cs` 读启动参数 | **已存在**，repro PASS |
| `WatchHistoryDialog` / `WatchLaterDialog` | 用 `ContentDialog` 实现（`MpvPlayerPage_History.cs`），不是独立类 | **已存在**，形态不同 |

## C. 已验证为误报（不要再查）

| 声称的缺陷 | 实测证据 | 结论 |
|---|---|---|
| `WUI2011` ×14 `MediaInfoUserControl` | `MediaInfoRow` 只有 getter、无 `INotifyPropertyChanged`；`ShowReport` 每次重建 `ItemsSource` | `OneTime` 正确 |
| `WUI2003` ×10 `Pivot` | `find-api` 报 308 成员；运行时以 `TabItemControl` 出现在 UIA 树 | 分析器规则错 |
| `menu-data` 为空 | 无媒体时 `[]`，`loadfile` 后 24 项（`diag_menu_after_load.py`） | 时序，非缺陷 |
| 导航栏播放时消失 | 设计如此，`ShellBackButton` 恢复 | 非缺陷 |
| 左边缘竖排文字 | 窗口投影，像素 (218,219,220) | 非缺陷 |
| 排序按钮溢出 | `delta=-56` | 非缺陷 |
| 首页 4 处"间距非令牌" | 同 `Grid` 内 `VerticalAlignment="Bottom"`，14px 是字体行高 | 非缺陷 |
| 182 个 `x:Key` 孤儿资源 | `audit-resource-keys.py` 四种读取方式，0 孤儿 | 非缺陷 |
| 三套皮肤控件位置完全一致 | 几何相同但**像素不同**（`scan-skins-pixels.py`，三个不同 hash） | 无缺陷；`Template` 改的是圆角/画笔，不改控件外框 |
| 皮肤设置切换到 `Center`/`Compact` 无效 | 上一行同一个误判的起因：脚本写的是 `Centre`/`Standard`，正确值是 `Center`/`Compact`（美式拼写，见 `AppSettings.PlayerStyle_*`） | 探针 bug，不是产品缺陷 |

## D. 需要先建立测量才能立项

| 域 | 缺什么测量 |
|---|---|
| Settings | 688 行的滚动、折叠、搜索、键盘导航 |
| Player | 三套皮肤（Centre/Compact/Standard）逐控件位置 |
| Player | 播放中/暂停/空闲三态的控制栏布局 |
| 历史与稍后看 | 列表项渲染、空态、滚动 |
| 菜单编辑器 | 树操作、拖拽、保存往返 |
| MediaInfo | 长值换行、复制、分节折叠 |
| 无障碍 | 8 状态 × 238 交互实例复测 |
| 高对比度 | 主题切换后对比度采样 |
| 窗口尺寸 | 窄窗/竖屏/超宽的分层 |

---

## 决策记录

### 2026-10-24 建立本任务

- **决定**：不修 `audit_domains.py`，在 `tools/ui-repair/` 建独立工具链
- **依据**：它一轮会话里四次把探针问题归因给产品；八域共享一进程而 IPC 每进程只服务
  一个连接，是结构性问题
- **放弃**：让它兼任验证器

### 2026-10-24 repro 判据必须是行为，不是名字

- **决定**：`repro.py` 全部改为查"能观察到应用在做这件事"，不查"上游用了什么标识符"
- **依据**：第一版四个 repro 全按类型名 grep，全部报"缺陷存在"。其中
  `SettingsPage.Actions.ImportExport.cs` 这个**文件名**含 `ImportExport`，而**内容里
  一次都没有**——它是 `partial class SettingsPage`，方法叫 `ImportConfigAsync`。
  按名字判断会让人去移植一个已经存在的功能
- **代价**：判据变宽（`ImportConfig|ExportConfig|AppConfData` 任一命中即算存在），
  可能放过部分实现。接受：宁可漏报，不可误报后白改

### 2026-10-24 间距审计口径

- **决定**：改为"同 `depth` 且竖直相邻"，并排除框架自绘控件
- **依据**：不加 `depth` 时把跨层级的 23px / 39px 报成问题
- **放弃**：`parentId`——snapshot 里没有这个字段

### 2026-10-24 几何相同不等于视觉相同

- **决定**：皮肤一致性改用像素比较（`scan-skins-pixels.py`），不用控件 rect
- **依据**：三套皮肤的控件 rect **完全一致**，但窗口像素是**三个不同 hash**。皮肤
  样式改的是 `Template` 内的圆角、画笔、按钮 chrome，控件外框不变
- **教训**：rect 只能证明"控件在哪"，不能证明"控件长什么样"。拿它判视觉一致性会得出
  "三套皮肤一模一样"，而事实相反
- **附带发现**：第一次写的皮肤脚本用了 `Centre`/`Standard`，而常量是
  `Center`/`Compact`，于是两次落在 `default` 分支、量到的是同一个皮肤。这类"探针自己
  设错了参数"的误判在本仓库已出现多次，写测量脚本时必须先确认参数取的是**代码里的
  常量**，不是记住的名字

---

## 格式约定

```
| `id` | domain | kind | confirmed/unverified | 一句话症状 | 复现命令或测量值 |
```

`unverified` 的项**不得**直接改产品代码——先按 `PLAYBOOK.md` 第 1 步证明它。
