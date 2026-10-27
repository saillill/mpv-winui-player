# 修好 windbg 插件不能被 DSH 加载的问题
#
# 问题：装好 windbg@win-dev-skills 后，11 个技能在 DSH 里调不到
#       （skill("windbg-user-exception-triage") 返回 unknown）。
#
# 根因（2026-10-27 实测）：
#   对比两个插件各自的 .claude-plugin/plugin.json——
#     winui  有 "skills": "./agent-plugin/skills/"        -> DSH 能加载
#     windbg 没有 skills 字段                             -> DSH 看不到
#   windbg 只有一个 skills.json，那是 GitHub Copilot 的清单格式
#   （{diagnosticSkills:[...], methodSkills:[...]}），claude-compat 不读它。
#   技能文件本身是标准的 skills/<name>/SKILL.md，frontmatter 合法，
#   位置也对——纯粹是 plugin.json 少一行声明。
#
# 这同时解释了为什么"重启 dsh"这个我原先给的建议是错的：重启不会让它出现，
# 因为清单里根本没有声明过这些技能。
#
# 修法：给 .claude-plugin/plugin.json 加 "skills": "./skills/"。
#
# 注意：改的是 ~/.claude/plugins/cache 下的文件，插件更新或重装会覆盖它。
# 所以这个脚本是幂等的——每次插件变动后重跑即可。
#
# 用法：
#   pwsh tools/mvpw-mcp/diagnostics/fix-windbg-plugin-skills.ps1
#   pwsh tools/mvpw-mcp/diagnostics/fix-windbg-plugin-skills.ps1 -Revert
#
# 改完需要让 DSH 重读技能目录：调用 reload-cc-plugins 技能（无需重启会话）。

param(
    [switch]$Revert
)

$ErrorActionPreference = 'Stop'
$pluginRoot = Join-Path $env:USERPROFILE '.claude\plugins\cache\win-dev-skills\windbg'
$failed = @()
$fixed = 0

if (-not (Test-Path -LiteralPath $pluginRoot)) {
    Write-Host "FAIL: windbg plugin is not installed at $pluginRoot"
    Write-Host "      Install it first: claude plugin install windbg@win-dev-skills"
    exit 2
}

# Every installed version gets the same treatment; a plugin update drops a new
# version directory and the old fix does not travel with it.
$manifests = Get-ChildItem -LiteralPath $pluginRoot -Directory |
    ForEach-Object { Join-Path $_.FullName '.claude-plugin\plugin.json' } |
    Where-Object { Test-Path -LiteralPath $_ }

if (-not $manifests) {
    Write-Host "FAIL: no .claude-plugin/plugin.json found under $pluginRoot"
    exit 1
}

foreach ($manifest in $manifests) {
    $version = Split-Path (Split-Path $manifest -Parent) -Parent | Split-Path -Leaf
    $backup = "$manifest.bak"

    if ($Revert) {
        if (Test-Path -LiteralPath $backup) {
            Copy-Item -LiteralPath $backup -Destination $manifest -Force
            Write-Host "  RESTORED $version from $([System.IO.Path]::GetFileName($backup))"
            $fixed++
        } else {
            Write-Host "  SKIP     $version -- no backup to restore from"
        }
        continue
    }

    $json = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($json.PSObject.Properties.Name -contains 'skills') {
        Write-Host "  OK       $version -- already declares skills=$($json.skills)"
        continue
    }

    # The skills live at <plugin>/skills/<name>/SKILL.md for this plugin. Verify that
    # before writing a pointer to it, so a wrong guess fails here rather than leaving
    # a manifest that claims skills it cannot find.
    $versionDir = Split-Path (Split-Path $manifest -Parent) -Parent
    $skillsDir = Join-Path $versionDir 'skills'
    $skillCount = @(Get-ChildItem -LiteralPath $skillsDir -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') }).Count
    if ($skillCount -eq 0) {
        Write-Host "  FAIL     $version -- no skills/<name>/SKILL.md under $skillsDir"
        $failed += $version
        continue
    }

    if (-not (Test-Path -LiteralPath $backup)) {
        Copy-Item -LiteralPath $manifest -Destination $backup -Force
    }
    $json | Add-Member -NotePropertyName 'skills' -NotePropertyValue './skills/' -Force
    $json | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $manifest -Encoding utf8 -NoNewline
    Write-Host "  FIXED    $version -- added skills='./skills/' ($skillCount skills found)"
    $fixed++
}

Write-Host ''
Write-Host ('=' * 58)
if ($failed.Count) {
    Write-Host "FAILED: $($failed -join ', ')"
    exit 1
}
if ($fixed -eq 0 -and -not $Revert) {
    Write-Host "Nothing to do -- every installed version already declares skills."
} else {
    Write-Host "Done. Now call the reload-cc-plugins skill (no dsh restart needed),"
    Write-Host "then skill('windbg-user-exception-triage') should resolve."
}
exit 0
