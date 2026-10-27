# windbg 插件为何在 DSH 里调不到 —— 找到了，也修了

> 2026-10-27。上一版 `PLUGINS-INSTALLED.md` 说"需要重启 dsh 才能 reconcile"。
> **那个判断是错的。** 重启也不会让它出现。这份文件记录真正的根因和修法。

---

## 1. 根因：插件清单少了一行声明

`windbg` 装在 `~/.claude/plugins/cache/win-dev-skills/windbg/1.0.0/`，11 个技能文件
完好无损、位置标准、frontmatter 合法，但 DSH 一个都看不到。

对比两个同市场的插件【已运行，读各自的 `.claude-plugin/plugin.json`】：

```jsonc
// winui（DSH 能加载）
{
  "name": "winui",
  "skills": "./agent-plugin/skills/",     // ← 这一行
  ...
}

// windbg（DSH 看不到）
{
  "name": "windbg",
  "skills.json 里有 diagnosticSkills: [...]  // ← 但这不是 claude 的格式
  ...
}
```

**`windbg` 的 `.claude-plugin/plugin.json` 里根本没有 `skills` 字段。**

它只有一个 `skills.json`，内容是这样的：

```json
{
  "name": "windbg",
  "version": "1.0.0",
  "diagnosticSkills": [ ... 10 项 ... ],
  "methodSkills": [ "windbg-diagnostic-method" ]
}
```

这是 **GitHub Copilot 的插件清单格式**（同一个包里还有个 `com.github.copilot/`
目录）。DSH 的 `claude-compat` provider 读的是 `.claude-plugin/plugin.json` 的
`skills` 字段，不读 `skills.json`。

所以技能文件一直在那儿，只是**没有任何声明指向它们**。

---

## 2. 所以"重启 dsh"是错建议

我上一轮写的是：

> `windbg` 需要重启（skill catalog 冻结）

**错。** 重启只是重新扫描目录——而目录里没有任何东西声明 `skills/`，扫多少遍都是空。
真正的原因不在缓存时机，在清单内容。

这也是为什么 `winui` 在我重新装它之前就能用、`windbg` 装了就是不出：差别不在安装
顺序，在 `plugin.json`。

---

## 3. 修法

给 `.claude-plugin/plugin.json` 加一行：

```json
"skills": "./skills/"
```

改完调 `reload-cc-plugins` 技能（热重载，**不需要重启会话**），11 个技能立刻出现在
目录里：

```
windbg-diagnostic-method
windbg-kernel-bugcheck-triage
windbg-kernel-irp-lifecycle-triage
windbg-kernel-lock-deadlock-triage
windbg-kernel-verifier-triage
windbg-user-exception-triage
windbg-user-heap-corruption-investigation
windbg-user-mutex-held-across-co-await
windbg-user-ttd-reverse-debugging-triage
windbg-user-virtual-memory-exhaustion
windbg-user-wait-chain-analysis
```

`skill("windbg-user-exception-triage")` 现在返回完整内容【已运行】。

### 3.1 固化成脚本

`tools/mvpw-mcp/diagnostics/fix-windbg-plugin-skills.ps1`

```powershell
pwsh tools/mvpw-mcp/diagnostics/fix-windbg-plugin-skills.ps1           # 修
pwsh tools/mvpw-mcp/diagnostics/fix-windbg-plugin-skills.ps1 -Revert   # 还原
```

**为什么要脚本而不是一次性手改**：改的是 `~/.claude/plugins/cache` 下的文件，
插件更新或重装会覆盖它。脚本是幂等的，每次插件变动后重跑即可。
脚本会先验证 `skills/<name>/SKILL.md` 确实存在再写指针，避免写出一个指向空气的声明。

往返测试通过【已运行】：`-Revert` 移除字段 → 重跑重新加上并报告 `11 skills found`。

---

## 4. 但修好了，它对本项目仍然没用

这是两件事，别混：

| 问题 | 状态 |
|---|---|
| 技能能不能被调用 | **修好了，能** |
| 技能对本项目有没有用 | **没有** |

`windbg-user-exception-triage` 的正文自己写着：

> Start here for access violations, heap corruption, stack overflow, fail-fast,
> breakpoints, and other structured exceptions in a **native** application...
> **Not for managed .NET exceptions, WinUI/XAML app errors, or kernel bugchecks.**

而关于 stowed exception，它明确承认：

> **Stowed/WinRT exceptions:** preserved error information can precede the final
> failure. Correlate its originating stack and nested errors with the application;
> **this package does not include a XAML extension workflow.**

**本项目唯一的崩溃类型正好是它排除的那一类，而且它自己说不提供 XAML 工作流。**

它的工作流是 `.exr -1` → `.ecxr` → `k` → `!analyze -v`，这需要 WinDbg 二进制，
本机装不上（非管理员 + 微软 CDN 403，见 `ENVIRONMENT-SETUP.md` §4）。

**结论不变**：崩溃归因用 `dotnet-dump` + CLR 的 `createdump`
（见 `CRASH-DUMPS.md`，已端到端验证到能指出异常类型和地址）。

---

## 5. 这个修复值不值得留

**值得，但理由不是"本项目要用它"。**

理由：它证明了插件不生效时该去查清单而不是猜缓存时机。DSH 的插件加载链路是
`installed_plugins.json` → `cache/<market>/<plugin>/<version>/` →
`.claude-plugin/plugin.json` 的 `skills` 字段。少了最后一环，前面全对也没用。

如果以后装别的插件发现"装了却没技能"，先跑这个脚本，或者用同样的方法看清单：

```powershell
# 一行诊断：这个插件的清单声明了 skills 吗
Get-ChildItem ~/.claude/plugins/cache/*/*/*/.claude-plugin/plugin.json -ErrorAction SilentlyContinue |
  ForEach-Object {
    $j = Get-Content $_.FullName -Raw | ConvertFrom-Json
    [pscustomobject]@{
      Plugin = $j.name
      Skills = if ($j.skills) { $j.skills } else { '(none declared)' }
    }
  } | Format-Table -AutoSize
```

---

## 6. 顺手发现的一个上游缺陷

`~/.claude/plugins/marketplaces/win-dev-skills/plugins/windbg/.claude-plugin/plugin.json`
**同样没有 `skills` 字段**【已运行】。

也就是说这不是缓存损坏，是**微软这个插件本身漏了声明**。任何用 claude 兼容格式读
技能的宿主都会遇到同样问题。如果以后要反馈，这是可复现的证据。
