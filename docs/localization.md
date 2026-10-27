# 本地化与菜单

用户可见文案只有一条通路：`AppLang` 属性 → `Languages/<lang>.json`。
菜单是第二条受同一条检查器约束的通路。数字可重算（`§6`）。

---

## 1. `AppLang` 的构成

`Lexicon/Language/` 下 9 个文件，共 **2196** 个属性：

| 文件 | 属性 | 行数 | 手改? |
|---|---:|---:|---|
| `AppLang.cs` | 1367 | 2063 | 是 |
| `AppLang.Gen.cs` | 712 | 724 | **否**（生成器覆盖，356 label + 356 desc） |
| `AppLang.Shell.cs` | 67 | 229 | 是 |
| `AppLang.Sections.Gen.cs` | 44 | 56 | **否** |
| `AppLang.MpvConfEditor.cs` | 6 | 30 | 是 |

属性格式固定为 `public string X { get; set; }`——**带空格**。
`AppLang` 的属性靠反射读取，正则漏掉空格会静默返回空集。

`CustomizeStrings.cs`（88 行）是**独立的伴随表**，不是 `AppLang` 成员：
它从语言 JSON 里按字符串键取值（`Get(key)`），因为 `AppLang` 靠反射读属性、
无法同时承载「没有对应属性的键」。设置界面的定制模式专用。

---

## 2. 九个语言文件

`src/mpvw-app/Languages/*.json`，运行时加载。

| 文件 | 键数 | 缺 `AppLang` 属性 | 多余键 | `Schema*` 键 |
|---|---:|---:|---:|---:|
| `de-DE` / `en-US` / `es-ES` / `fr-FR` / `ja-JP` / `ko-KR` / `ru-RU` / `zh-TW` | 各 1526 | **712** | 42 | 0 |
| `zh-CN` | 2238 | **0** | 42 | **712** |

两个事实：

1. **只有 `zh-CN` 是完整的。** 其余 8 个各缺 712 个 `Schema*` 键——那正好是生成层
   `AppLang.Gen.cs` 的全部属性。缺的这 712 个键在界面上回落成英文 schema 文本。
2. **每个文件都有 42 个「多余键」**（没有对应 `AppLang` 属性）。这是设计：
   41 个 `Customize*` + 1 个 `Apply*` 属于上面那张伴随表。
   `check-localization` 把它们归为 `expected`，不是错误。

缺 `Schema*` 这一项已从 ERROR 降级为 **WARNING**（`check-localization.py:209-219`），
所以默认九文件口径现在退出码 0。窄口径 `--languages=zh-CN,en-US` 同样 0。

---

## 3. 加载与校验

### 3.1 路径

| 用途 | 路径 |
|---|---|
| 内置语言文件 | `<AppContext.BaseDirectory>\Languages\<code>.json` |
| 用户覆盖 | `%LOCALAPPDATA%\mpv-winui\languages` |
| 默认语言 | `en-US`（`LanguageManager.DefaultLanguage`） |

用户目录优先于内置（`UserLanguageDirectory`，`LanguageManager.cs:17-21`）。
`GetAvailableLanguages` 枚举内置目录，过滤掉不安全的文件名；
目录缺失时回落内置的 `[en-US, zh-CN]`。

### 3.2 `LanguageCode.IsSafe` 为什么存在

语言代码来自持久化的 `CurrentLanguage` 设置，曾被直接拼进
`Path.Combine(userDir, code + ".json")`。一个 `..\..\some` 就能让应用打开
语言目录之外的文件——读的只是一次 JSON 解析，但「打开哪个路径」不该由存储的字符串决定。

规则：只接受 ASCII 字母数字，`xx` 或 `xx-YY` 形状，允许更多子标签
（`zh-Hans-CN`），总长 ≤ 64，每段长度 1..8。分隔符、点、冒号、空格、盘符一律拒绝。

它单独成文件、不依赖 WinUI 与 `AppContext`，所以 `mpv-conf-test` 能链接它并直接断言
——与 `MpvProtocolUri`、`MpvConfValueCodec` 同样的理由。

### 3.3 切换语言时发生什么

`AppContext.SwitchLanguage(code)`：

1. `LanguageManager.Normalize(code)` 归一化（校验也在这里，因为值会被持久化并
   插值进 mpv 命令）
2. 写 `AppSetting.CurrentLanguage`
3. `LanguageManager.Load(AppLang, lang)`
4. `SendMpvCommand($"set user-data/mpvw/language {lang}")` —— 让 Lua 菜单脚本
   重新解析自己的翻译表
5. 触发 `LanguageChanged`

第 4 步是必要的：`dyn_menu.lua` / `dynamic_menu.lua` / `select.lua` 各自内置 8 语言表，
它们通过 `user-data/mpvw/language` 这个属性知道该用哪一份。

---

## 4. 菜单

### 4.1 `menus.json`

`src/mpvw-app/Menus/menus.json`——**顶层是数组**，7 个条目，递归共 **67 个节点**。
每个节点：

| 字段 | 含义 |
|---|---|
| `id` | 稳定标识 |
| `labelKey` | 必须是 `AppLang` 的属性名 |
| `action` | 必须是 `RegisterMenuAction("…")` 注册过的 id |
| `children` | 子菜单（可选） |

结构受 `tools/menus.schema.json` 约束，由 `check-localization.py` 内联校验
（不依赖 `jsonschema` 包）。

### 4.2 用户覆盖

`MenuDefinitionSource.UserPath` = `%LOCALAPPDATA%\mpv-winui\mpv\menus.json`。
**用户目录优先于内置**，且 `ConfigDeployer` 把 `menus.json` 列进 `UserOwnedFiles`、
`deploy-config.ps1` 把它排除在 robocopy `/MIR` 之外——否则部署会删掉用户的自定义。

### 4.3 另一条菜单通路：`input.conf` 的 `#menu:`

播放页的右键菜单来自 `dyn_menu.lua` 的 `menu-data` 属性，项目按 `input.conf` 里的
注释顺序渲染（与 mpv-menu-plugin 一致），纯文本、无图标、不重命名。
要隐藏条目直接改 `input.conf`。

---

## 5. 改文案的规则

| 规则 | 依据 |
|---|---|
| 所有用户可见文案必须走 `AppLang` | 不接受在 XAML 或 C# 里写死字符串 |
| 新文案至少写 `en-US.json` + `zh-CN.json` | 其余 7 种回落英文字面量；缺失会被检查器报为 hand-written 缺失 |
| 改 `.Gen.` 属性无效 | 生成器下次覆盖；改生成器或 `MpvConfOptions/mpv-options.json` |
| `CustomizeStrings` 的键没有 `AppLang` 属性是设计 | 检查器按 `Customize*` + `Apply*` 归为 expected，不要「修」 |
| 改完跑 `python tools/check-localization.py` | 2196 属性 × 9 文件 + `menus.json` 结构 + 动作白名单 |

---

## 6. 重算本文每个数字

```powershell
# 每个 AppLang 文件的属性数与行数
python -c "
import re, glob, os
for f in sorted(glob.glob('src/mpvw-app/Lexicon/Language/AppLang*.cs')):
    t = open(f, encoding='utf-8-sig').read()
    print(os.path.basename(f),
          len(re.findall(r'public\s+string\s+\w+\s*\{\s*get;\s*set;\s*\}', t)),
          t.count(chr(10)))
"                          # 合计 2196

# 每种语言的缺口
python -c "
import json, glob, os, re
props = set()
for f in glob.glob('src/mpvw-app/Lexicon/Language/AppLang*.cs'):
    props |= set(re.findall(r'public\s+string\s+(\w+)\s*\{\s*get;\s*set;\s*\}', open(f, encoding='utf-8-sig').read()))
for f in sorted(glob.glob('src/mpvw-app/Languages/*.json')):
    d = json.load(open(f, encoding='utf-8-sig'))
    print(os.path.basename(f), len(d), 'missing', len(props - set(d)),
          'extra', len(set(d) - props),
          'schema', sum(1 for k in d if k.startswith('Schema')))
"

# 菜单节点数
python -c "
import json
d = json.load(open('src/mpvw-app/Menus/menus.json', encoding='utf-8-sig'))
def walk(a):
    return sum(1 + (walk(x['children']) if isinstance(x, dict) and isinstance(x.get('children'), list) else 0) for x in a)
print('top', len(d), 'total', walk(d))       # top 7, total 67
"

# CustomizeStrings 行数
(Get-Content src\mpvw-app\Lexicon\Language\CustomizeStrings.cs).Count      # 88

python tools/check-localization.py                                          # 2196 props, 9 files
python tools/check-localization.py --languages=zh-CN,en-US                   # 窄口径
```

---

## 7. 原因不明

- **为什么只有 `zh-CN` 补齐了 712 个 `Schema*`**：其余 8 种（含 `en-US`）为 0。
  `check-localization` 的注释把缺口登记为已知例外，但"为什么 default 语言 `en-US`
  也没有"没有说明——生成行的标签本来就取自英文 schema 文本，所以 `en-US` 缺键时
  界面上看到的中英文都正确，缺口在别的语言才显形。这个解释是从行为推的，
  代码里没有写。
- **`AppLang.cs`（1367 属性 / 2063 行）为什么持续手写增长而不拆分**：它已有
  `Shell` / `MpvConfEditor` 两个 partial，但主体仍是单文件。
