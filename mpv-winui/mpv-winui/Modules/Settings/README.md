# 分区 2 · 设置 / Settings

> **改这个区之前必须读 `docs/settings-design-contract.md`（74 KB）。**
> 它是本区唯一权威：结构、命名、分组规则、UI 与交互约束、强制验证流程、
> 33 条已知例外。本 README 只是索引，不能替代它。

## 职责

设置窗口与设置页：6 个 category、69 个 section、**688 行**选项
（326 手写 + 362 schema 生成）、布局定制（重排/重命名/隐藏/建文件夹）、
mpv.conf 的双向同步。

**不负责**：`mpv.conf` 的文本编辑界面（`Modules/MpvConf`）、菜单编辑器（`Modules/Menu`）。

## 数字（2026-10-22 实测，`tools/report-doc-numbers.py`）

| 项 | 值 |
|---|---:|
| category | 6（program / video / audio / subtitles / playback / interface） |
| section | 69 |
| 选项行合计 | **688** = 326 手写 + 362 生成 |
| 生成行的 `SchemaLabel_*` + `SchemaDesc_*` | 724（362 + 362） |
| `AppSettings` 属性 | 701 |
| mpv 选项映射 | 633 |

## 入口文件

| 文件 | 作用 |
|---|---|
| `SettingsWindow.xaml(.cs)` | 窗口壳（`WindowsManager` 单例） |
| `SettingsPage.xaml(.cs)` | 页面根（70 KB） |
| `SettingsPage.Options.cs` | **树构建器 `BuildSettings`** + 四张表：`sectionMap`（唯一来源）、`sectionOrder`、`optionOrder`、`categoryOrder` |
| `SettingsPage.Options.Gen.cs` | **生成文件，禁止手改**（362 行，`tools/gen-schema-options.py`） |
| `Modules/Settings/Layout/SettingsSections.cs` | category / section 的 Id、图标、标题、描述 |
| `Modules/Settings/Layout/SettingsLayout.cs` | 用户定制的数据模型 + `SettingsLayoutStore` |
| `Modules/Settings/Controls/Option*.xaml(.cs)` | 每种 `OptionType` 一个渲染器 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `AppSettings` | 全部设置属性（注册表持久化） | 全应用 |
| `MpvSettings` | `ToCommand(key)` | `AppContext.SendMpvCommand` |
| `MpvConfOverrides` | `Refresh()` / 查询"该选项是否被 mpv.conf 接管" | `Player` |
| `AppContext` | `WriteManagedMpvConfig()` | 任何改设置的地方 |

## 依赖

- 依赖：`MpvConf/Conf`（读写 mpv.conf）、`Language`、`Common/Utils`（`AtomicFile`、`ConfigWriteQueue`）。
- **不依赖**：`Player`、`Menu`、`MediaInfo`。

## 硬规则（违反会静默出错）

1. **两半树都要读**：手写 partial + `.Gen.cs`。历史上 6 个检查器只读一半，
   报出几万个幻影发现。
2. **`sectionMap` 是 section 的唯一来源**：少一条 → `BuildSettings` 抛异常（故意的）。
3. **`OptionType`(11) → `OptionTemplateSelector` → `OptionListControl` 的 `DataTemplate`
   是 1:1:1**：加一种类型要改三处。
4. **折叠 section 要同步三处**：`Option.AdvancedSection`、`FoldedSections`、
   `OptionListControl.CollapsedSectionIds`（后者是 sticky 依赖属性，每次赋值 `OptionList`
   都要清）。
5. **改 `.Gen.` 文件无效**：改生成器或 `MpvConfOptions/mpv-options.json`。

## 改完必须跑的检查器

```bash
python tools/check-sections.py
python tools/check-localization.py
python tools/check-settings-drift.py
python tools/check-empty-sections.py
python tools/diagnose-advanced-flag.py
python tools/check-conf-lines.py
```

## 上下文裁剪提示

改某一行选项 → 先 `grep -n "nameof(AppContext.AppSetting.你的属性)" SettingsPage.Options.cs`
找到它的 `sectionMap` 条目，再改对应 `Options.<域>.cs`。**不要通读 `SettingsPage.Options.cs`。**
改生成的选项 → 只碰 `tools/gen-schema-options.py` + `mpv-options.json`，然后 `--check`。
