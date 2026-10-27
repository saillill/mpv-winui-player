# 分区 6 · 本地化 / Language

## 职责

9 种语言的词汇表。`AppLang` 是**唯一的用户可见文案来源**，`Languages/*.json` 是
运行时加载的译文。

## 实测数字

| 文件 | 属性数 | 可手改 |
|---|---:|---|
| `AppLang.cs` | 1367 | 是 |
| `AppLang.Gen.cs` | 712 | **否**（生成器覆盖，356 label + 356 desc） |
| `AppLang.Shell.cs` | 67 | 是 |
| `AppLang.Sections.Gen.cs` | 44 | **否** |
| `AppLang.MpvConfEditor.cs` | 6 | 是 |
| **合计** | **2196** | |

9 个 JSON 各 1526 键，`zh-CN.json` 2238（多出的 **712** 是 `Schema*` 译文，
其余 8 种语言这些键为 0）。缺口与后果见 [`docs/localization.md`](../../../../docs/localization.md) §2。

## 入口文件

| 文件 | 作用 |
|---|---|
| `LanguageManager.cs` | 加载 / 归一化语言代码 / 发 `LanguageChanged` |
| `LanguageFile.cs` | JSON 解析（一次解析成字典，不反射） |
| `LanguageCode.cs` | 语言代码常量与 `Normalize` |
| `AppLang*.cs` | 词汇表（见上表） |
| `CustomizeStrings.cs` | **独立伴随表**，不是 `AppLang` 成员（设置定制模式专用，49 键） |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `AppContext.AppLang` | 全部属性 | 全应用 |
| `AppContext.SwitchLanguage(code)` | 切语言 + 通知 mpv | 设置页 |
| `LanguageManager.GetAvailableLanguages()` | 枚举 `Languages/*.json` | 设置页 |

## 依赖

- **不依赖任何功能域**。它是词汇表，任何区都可以用它，它不能用任何区。

## 硬规则

1. **所有用户可见文案必须走 `AppLang`**，不准在 XAML 或 C# 里写死字符串。
2. **新文案只写 `en-US.json` + `zh-CN.json`**（其余 7 种由后续翻译跟进；
   缺失会被 `check-localization.py` 报为 hand-written 缺失）。
3. **属性格式是 `public string X { get; set; }`，带空格**。正则漏空格会静默返回空集。
4. **`CustomizeStrings` 的键没有 `AppLang` 属性是设计**，`check-localization` 会报
   "expected"，不要"修"它。

## 改完必跑

```bash
python tools/check-localization.py    # 2196 属性 × 9 文件
```

## 上下文裁剪提示

加一条文案 → 只读 `AppLang.Shell.cs`（67 属性）或对应 partial 的相邻 10 行，
再改 `en-US.json` + `zh-CN.json`。**不要打开 `AppLang.cs`（164 KB）通读。**
