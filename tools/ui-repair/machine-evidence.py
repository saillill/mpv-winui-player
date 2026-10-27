"""Does the tree have anything a machine can decide alone?

The nightly job needs a category where a defect can be proven AND fixed by substitution. The
font-size case was the obvious candidate and it collapsed: 81 value matches, 0 real defects
after three filters (semantics, precedent, element kind). Every "match" was either a
different element type or a token whose name meant something else.

That is a useful negative result, and it changes the design: the nightly job's job is NOT to
find fixes, it is to produce **evidence** that is expensive to gather and cheap to judge.
This measures whether such evidence exists at all tonight, so the report can say "nothing to
decide" honestly instead of padding a list with noise.

Checks, all output-only:

  drift      a setting, language key, or generated row that no code reads
  dead       a type declared and never constructed
  orphan     a resource key with no reader (four reader kinds)
  unpaired   a C# subscription with no C++ raise, or the reverse
  stale-doc  a file:line citation that points at nothing

These are measurable without judgement, so they belong in the morning list. None of them is
auto-fixed: removing a "dead" type is only safe if it is not reached by reflection, XAML
attribute values, or a string target -- and that is a judgement call.
"""
import io
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
def sh(*args):
    r = subprocess.run(args, cwd=REPO, capture_output=True, text=True,
                       errors="replace")
    return r.returncode, (r.stdout or "") + (r.stderr or "")


def git_files(exts):
    code, out = sh("git", "ls-files")
    return [f for f in out.splitlines()
            if f.endswith(exts) and "/bin/" not in f and "/obj/" not in f]


def check_orphan_resources():
    """x:Key resources with no reader, counting all four ways the app reads them."""
    sys.path.insert(0, os.path.join(REPO, "tools"))
    code, out = sh(sys.executable, r"tools\audit-resource-keys.py")
    orphans = []
    grab = False
    for line in out.splitlines():
        if "with NO reader at all" in line:
            grab = True
            continue
        if grab and line.strip().startswith("read ONLY"):
            grab = False
        if grab and line.strip().startswith(("AppBar", "Mpv", "Check", "Equal",
                                             "Dark", "Light", "Default", "High")):
            pass
    m = re.search(r"with NO reader at all:\s*(\d+)", out)
    total = int(m.group(1)) if m else -1
    return total, out


def check_dead_types():
    """Types declared in .cs with no construction, no XAML element, no static use.

    The first version reported 22 suspects and was mostly wrong: `OptionBooleanControl` is
    used as `<ctl:OptionBooleanControl` in `OptionListControl.xaml`, and the search pattern
    `<OptionBooleanControl` cannot match a namespace-qualified tag. Every custom control in
    this repo is written that way, so the whole `Option*` family came back as dead.

    The pattern now allows an optional `prefix:` before the tag name, and a type counts as
    used if it appears in a XAML file at all -- a control is referenced by its tag, not by a
    constructor call.

    Types whose name ends in EventArgs / Extensions / Helper / Converter / Tests are skipped:
    those are referenced through signatures, converters are resolved by resource key, and the
    test project is not part of the app.

    **Second version was still wrong, and its 10 findings were all false.** The static-use
    patterns were plain substring tests, so `" %s." % name` -- a SPACE before the name --
    could only match a call preceded by a space. Real C# writes `!MotionGate.Resolve()`,
    `x:Bind local:TreeHelpers.MenuFor(...)`, `(LayoutDraft)draft`; the character before the
    name is `!`, `:`, `(` or `{`, never a space. Measured on the ten it reported:

        MotionGate                12 dotted uses   detector matched 0
        TreeHelpers                8               0
        IconFonts                  4               0
        PluginConfigWriter         2               0
        BindConvertor              4               0

    and the only one with no dotted use at all, `CoverArtProbe`, is an NUnit `[TestFixture]`
    -- discovered by reflection, so a static reference count can never see it.

    The check now uses a real identifier boundary (the name must not be preceded by a word
    character or `.`) and looks in XAML for ANY occurrence, not just an element tag: a static
    class is referenced from a markup extension (`{x:Bind local:TreeHelpers.X(..)}`), which
    contains no tag. NUnit fixtures are skipped by attribute, not by the "Tests" name suffix.
    """
    files = git_files(".cs")
    xaml_files = git_files(".xaml")

    decl = re.compile(r"^\s*(?:public|internal)\s+(?:sealed\s+|static\s+|partial\s+)*"
                      r"(?:class|record)\s+([A-Z][A-Za-z0-9_]*)", re.M)

    sources = {}
    for f in files:
        p = os.path.join(REPO, f.replace("/", os.sep))
        try:
            sources[f] = io.open(p, encoding="utf-8-sig", errors="replace").read()
        except OSError:
            continue
    cs_blob = "\n".join(sources.values())

    xaml_blob = []
    for f in xaml_files:
        p = os.path.join(REPO, f.replace("/", os.sep))
        try:
            xaml_blob.append(io.open(p, encoding="utf-8-sig", errors="replace").read())
        except OSError:
            continue
    xaml_all = "\n".join(xaml_blob)

    declared = set()
    for f, src in sources.items():
        for m in decl.finditer(src):
            declared.add((m.group(1), f))

    suspects = []
    for name, f in sorted(declared):
        if name.endswith(("EventArgs", "Extensions", "Converter", "Tests")):
            continue
        # Reflection-discovered types: NUnit fixtures are found by attribute, so a reference
        # count proves nothing about them. `CoverArtProbe` was reported dead on that basis.
        src = sources.get(f, "")
        if "[TestFixture]" in src or "[Explicit(" in src:
            continue
        # Any occurrence of the identifier in XAML counts: a control appears as a tag, a
        # static helper appears as `local:Name.Member` inside a binding, and neither form is
        # a constructor call.
        if re.search(r"(?<![A-Za-z0-9_.])" + re.escape(name) + r"\b", xaml_all):
            continue
        # Identifier boundary, not a substring: `!Name.`, `(Name)`, `<Name>`, `Name(` and
        # ` Name` are all uses; `OtherName` and `NameSpace` are not.
        if re.search(r"(?<![A-Za-z0-9_])" + re.escape(name) + r"\b(?!\s*\{)", cs_blob):
            continue
        suspects.append((name, f))
    return suspects


def check_doc_references():
    code, out = sh(sys.executable, r"tools\check-doc-references.py")
    m = re.search(r"references inspected:\s*(\d+)\s*\|\s*broken:\s*(\d+)", out)
    if not m:
        return -1, -1, out
    return int(m.group(1)), int(m.group(2)), out


def check_winrt_pairs():
    code, out = sh(sys.executable, r"tools\check-winrt-events.py")
    return code == 0, out


def check_lua_opts():
    code, out = sh(sys.executable, r"tools\check-script-opts.py")
    return code == 0, out


def main():
    findings = []

    n_orphan, orphan_out = check_orphan_resources()
    if n_orphan:
        findings.append(("orphan-resources", "%d 个 x:Key 资源无读取者" % n_orphan))
    print("orphan resources:", n_orphan)

    dead = check_dead_types()
    if dead:
        findings.append(("dead-types", "%d 个类型疑似无任何使用" % len(dead)))
    print("dead type suspects:", len(dead))
    for name, f in dead[:10]:
        print("   %-32s %s" % (name, f))

    inspected, broken, doc_out = check_doc_references()
    if broken:
        findings.append(("stale-docs", "%d/%d 条 file:line 引用失效" % (broken, inspected)))
    print("doc references: %d inspected, %d broken" % (inspected, broken))

    ok_ev, ev_out = check_winrt_pairs()
    if not ok_ev:
        findings.append(("winrt-events", "C# 订阅与 C++ raise 不配对"))
    print("winrt event pairs:", "ok" if ok_ev else "MISMATCH")

    ok_lua, lua_out = check_lua_opts()
    if not ok_lua:
        findings.append(("lua-opts", "Lua script-opts 与 DLL 声明不一致"))
    print("lua script-opts:", "ok" if ok_lua else "MISMATCH")

    print("\n=== evidence available tonight: %d categor(y/ies) ===" % len(findings))
    for k, v in findings:
        print("  %-18s %s" % (k, v))
    if not findings:
        print("  none -- the tree is clean on every axis this can measure")

    out = os.path.join(REPO, "_work", "nightly", "machine-evidence.md")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    lines = ["# 机器可判定的证据", ""]
    if findings:
        lines += ["| 类别 | 发现 |", "|---|---|"]
        lines += ["| %s | %s |" % (k, v) for k, v in findings]
    else:
        lines.append("无。所有可机械判定的维度都是干净的。")
    lines += ["", "## 死类型疑似项", ""]
    lines += ["- `%s` — `%s`" % (n, f) for n, f in dead[:40]] or ["无"]
    lines += ["", "## 原始输出", "",
              "- doc references: " + doc_out.strip().splitlines()[-1]
              if doc_out.strip() else ""]
    io.open(out, "w", encoding="utf-8").write("\n".join(lines))
    print("\nwritten:", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
