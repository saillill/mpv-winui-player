# BASELINE：判断"什么是错的"的依据

改动 UI 前先在这里找到依据。找不到就先补一条依据，再动手。

---

## 0. 三个来源，按优先级

| 优先级 | 来源 | 怎么查 |
|---|---|---|
| 1 | WinUI 3 / Windows App SDK 官方 | `winapp find-api`、`winapp find-ui` |
| 2 | 上游 `ikas-mc/mpv-winui-player` | `git fetch` 后 `git show FETCH_HEAD:<path>` |
| 3 | 本仓库已定义的令牌 | `Styles.xaml` 的 `Mpv*` 键、`MotionTokens.cs` |

冲突时按此顺序取高者。三者都没有 → **不是"随便选一个"**，而是先在本文补一行依据。

---

## 1. 官方规范

### 1.1 查 API 真实形状（不要凭记忆）

```bash
winapp find-api members <Type> --project-dir mpv-winui/mpv-winui
winapp find-api type <Type> --json --project-dir mpv-winui/mpv-winui
```

**必须用项目参数**：不带 `--project-dir` 时它会用别的 SDK 版本，答案会错。

已知教训：分析器规则 `WUI2003` 声称 `Pivot` 是 UWP-only、WinUI 3 无等价物。实测
`Microsoft.UI.Xaml.Controls.Pivot` 有 308 个成员，且运行时以 `TabItemControl` 出现在
UIA 树里。**分析器的规则会错，`find-api` 不会。**

### 1.2 查官方控件示例

```bash
winapp find-ui <关键词>          # 从 WinUI Gallery 找示例
```

优先用官方控件，不要用手搭的等价物。已知反例：不要用 `Border` 拼卡片，用
`MpvCardStyle`；不要用 `StackPanel` 模拟导航，用 `NavigationView`。

### 1.3 主题与颜色

- 颜色一律 `ThemeResource`（如 `TextFillColorSecondaryBrush`），不写字面色
- 文字样式用官方 `*TextBlockStyle`（`CaptionTextBlockStyle`、`BodyTextBlockStyle`…）
- 例外：`MpvConfTextWindow.xaml` 的灰色字面量是**刻意的**——那个编辑器在所有主题下
  都是深色，令牌色在深色底上渲染结果相同

---

## 2. 上游实现

真正的上游是 `ikas-mc/mpv-winui-player`，**不是** `cloud`（`cloud` 就是本仓库自己的
`saillill` 镜像，推送到那里不会带来任何新东西）。

```bash
git fetch https://github.com/ikas-mc/mpv-winui-player.git main --depth=1
git show FETCH_HEAD:mpv-winui/mpv-winui/Modules/Player/...
```

规模对照（2026-10-24 实测）：上游 179 个源文件，本地 275 个。差 96 个，其中一部分是
本地自研，一部分是上游有而本地缺失的能力。

### 2.1 上游目录结构（用于快速定位）

```
Modules/ActivationRegistration/   文件/协议关联
Modules/AppConfData/              配置导入
Modules/Player/BuiltInPreview/    内置预览
Modules/Player/History/           历史与稍后看
Modules/Menu/MenuEditor/          菜单编辑器
```

### 2.2 对齐判断表

| 情况 | 动作 | 记录位置 |
|---|---|---|
| 上游有、本地无、通用能力 | 移植，保留版权头 | `QUEUE.md` 的移植记录 |
| 上游有、本地有、行为不同 | 对齐上游（除非本地差异有理由） | 台账写理由 |
| 上游无、本地自研有用 | 保留，标"本地扩展" | 台账 |
| 上游已删、本地还留 | 清理，先证零引用 | 台账 |

**清理前**：`git grep` 确认零引用，且排除反射与 `x:Name` 使用。这个仓库删过两次
`Styles.xaml` 的内容并回滚过两次，就是因为没查够。

---

## 3. 本地令牌（唯一允许的魔数来源）

### 3.1 间距与尺寸（`Styles.xaml`）

已定义 30+ 个 `Mpv*` 键。常用的：

| 类别 | 键 |
|---|---|
| 间距 | `MpvSpaceXxs` `MpvSpaceXs` `MpvSpaceCompact` `MpvSpaceS` `MpvSpaceM` `MpvSpaceL` |
| 卡片 | `MpvCardPadding`(16,16,16,16) `MpvCardStyle` `MpvCardCornerRadius` |
| 图标 | `MpvInlineIconSize` `MpvIconFontFamily` |
| 导航 | `MpvNavPaneWidth` `MpvNavPaneRailWidth` `MpvNarrowLayoutThreshold` |
| 内容 | `MpvContentMaxWidth` |
| 选项行 | `MpvOptionControlGap`(24) `MpvOptionRowMargin` `MpvOptionRowMarginXl` |
| 文本样式 | `MpvOptionLabelStyle` `MpvOptionDescriptionStyle` `MpvOptionErrorStyle` |

完整清单：

```bash
git grep -o 'x:Key="Mpv[A-Za-z]*"' -- 'mpv-winui/mpv-winui/Styles/Styles.xaml' | sort -u
```

**审计过**：182 个 `x:Key` 资源全部有读取者，0 个孤儿
（`python tools/audit-resource-keys.py`，它统计 `{StaticResource}`、
`TryGetValue("...")`、`FindName("...")` 和裸字符串四种读取方式）。

### 3.2 动效（`Modules/Motion/MotionTokens.cs`）

| 令牌 | 值 | 用途 |
|---|---|---|
| `ShortMs` | 83 | 小控件状态切换 |
| `MediumMs` | 167 | 面板进出（默认） |
| `LongMs` | 250 | 大面积转场 |
| `EnterControl1/2` | (0,0)→(0,1) | 入场曲线 |
| `MoveControl1/2` | (0.55,0.55)→(0,1) | 移动曲线 |
| `ExitControl1/2` | (1,0)→… | 出场曲线 |

83/167/250ms 是 Fluent 的标准三档。文档在 `docs/motion-system.md`。

**不要用 XAML Storyboard 做交互反馈**——那个文档解释了原因，且实现走
`MotionBehaviors` + `SpringMath`。

### 3.3 位移用 `Translation`，不是 `Offset`（2026-10-24 更正）

前一条写的是"位移用 `Translation`"，但实测：

- `UIElement.Translation` 是 `System.Numerics.Vector3`，XAML **没有资源形式**，
  只能写字面量（`PlayerControl.xaml` 用 `Translation="0,0,32"` 两处，带注释）
- `RenderTransform` / `Offset` 仍用于布局内位移

所以规则是：**阴影/景深用 `Translation` 字面量并注释；布局位移用 `Translation` 的
`Vector3` 构造或 `RenderTransform`**，具体看 `MotionBehaviors` 现有写法。

---

## 4. 设置区

`docs/settings-design-contract.md` 是设置区的**唯一权威**（73 KB，含 33 条已知例外
登记）。改 `Modules/Settings/` 之前必须读它。

冲突时以它为准，本文让位。

---

## 5. 补充依据的格式

本文没有覆盖的情况，在这里加一节，格式：

```markdown
### N.M <问题>

**依据**：<官方规范链接 / 上游 commit:path / 令牌键>

**结论**：<怎么做>

**反例**：<不要怎么做，以及为什么>
```

有了这一节，下一个人（或下一轮的你）不用再重新论证。
