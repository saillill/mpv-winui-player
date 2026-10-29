# 10 · 国际化：只保留接口，不做翻译

> **决策（用户明确指示）**：国际化**暂缓**，**不做任何翻译**。
> 本文只记录"接口形态"，用途是：将来若要做，现有结构不会成为障碍。
> **不包含**译法、术语表、翻译平台、语言扩展方案。

---

## 一、现状（实测）

| 项 | 事实 |
|---|---|
| 语言文件 | `src/mpvw-app/Languages/*.json`，**9 份** |
| 词条规模 | zh-CN 单份约 **2240 个属性**（用户口径） |
| 生成机制 | `AppLang.Gen.cs`（`AppLang.cs` + `AppLang.Gen.cs` + `AppLang.Sections.Gen.cs` + `AppLang.MpvConfEditor.cs` + `AppLang.Shell.cs`） |
| 检查器 | `tools/check-localization.py`：核对 `AppLang` 属性 ↔ 9 个 JSON，缺失即报 |
| 运行时 | `LanguageManager.cs`、`LanguageFile.cs`、`LanguageCode.cs`；`AppContext.AppLang` 单点暴露 |
| 切换机制 | `AppContext.LanguageChanged` 事件；控件在 `ApplyLocalizedStrings()` 里重设文字 |
| 既有缺口 | de-DE 完成度 94%（2055/2196），残留项见 `check-localization.py` 输出 |

---

## 二、接口形态（保留即可，不需改动）

### 2.1 三层契约（已存在，且有检查器盯着）

```
AppLang 属性（C# 强类型）
      ↕  check-localization.py 核对
Languages/*.json（9 份，同名 key）
      ↓
UI：要么 XAML 字面量（少数），要么 ApplyLocalizedStrings() 赋值（多数）
```

**这是本仓库最好的一条契约**：`AppLang` 属性与 JSON key 的一致性由检查器强制，
新增词条漏写 JSON 会立刻变红。**暂缓国际化不需要拆掉它。**

### 2.2 保留的意义

- `AppLang` 是 C# 强类型属性，**不是** `x:Uid` + `.resw`。
  这套结构在单语言下零成本（属性就是字符串），在将来要加语言时也不用重构 UI 层
- 若将来改成 `.resw` + `x:Uid`，会**破坏**现有 `ApplyLocalizedStrings` 模式
  （该模式在 30 多个控件里使用），属于高成本迁移
- **结论：保持现状即可，无需任何动作**

### 2.3 唯一需要注意的约束

新增 UI 文字时，**仍应走 `AppLang` 属性**而不是硬编码字符串。
理由不是"为国际化做准备"，而是：`check-localization.py` 只覆盖走 `AppLang` 的属性；
硬编码文字既不进检查器，也不进任何语言文件，将来清理时无从定位。

---

## 三、适用性评估

| 线索 | 可用程度 | 接入成本 | 说明 |
|---|---|---|---|
| 保持 `AppLang` + JSON 三层契约 | **高** | **0** | 现状即最优；不需要任何改动 |
| 引入 `.resw` + `x:Uid` | 低 | 极高 | 会推翻 30+ 处 `ApplyLocalizedStrings`，且用户已明确要求 **不引入** |
| 补齐 de-DE 的 6% 缺口 | 低 | 中 | **属于翻译工作，本次明确不做** |
| 翻译平台（Crowdin / Weblate 等） | — | — | **本次不调研**，按用户指示 |

---

## 四、检索提示词（留作将来，现在不用）

> 以下仅为将来若解除暂缓时的入口，本次**不执行**。

### 4.1 搜索式

```
WinUI 3 localization resw x:Uid vs strongly typed resource class
WinUI 3 runtime language switch without restart
json based localization .NET source generator
```

### 4.2 交给将来会话的提示词

> 你在一个 WinUI 3 + C# 的 mpv 播放器仓库（`mpvw`）。
> 国际化采用 **C# 强类型属性 + 9 份 JSON** 的结构，由 `tools/check-localization.py` 强制一致，
> UI 侧通过 `ApplyLocalizedStrings()` 赋值，语言切换走 `AppContext.LanguageChanged`。
>
> **不要**改成 `.resw` + `x:Uid`：会破坏 30 多处 `ApplyLocalizedStrings`。
>
> 当前任务（若已解除暂缓）：补齐 de-DE 的缺失词条。先回答：
> 1. 缺失的 141 条是哪些？按分区归类。
> 2. 是否有词条是"故意不译"（品牌名、格式串）？如何标记？
> 3. `check-localization.py` 的 COMPLETENESS 输出能否直接作为待译清单？
