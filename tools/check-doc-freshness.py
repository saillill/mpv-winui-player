"""Do the docs still describe the code that exists, or a tree that has moved on?

Three rules, each for a defect this repo actually accumulated:

  R1  A commit hash written into prose (`基线 abc1234`) rots on the next commit.
      A document cannot keep a hash true, so it must not state one at all --
      the command that DERIVES the answer belongs in a code block instead.

  R2  Historical narration hides removed features under a live heading. "曾经是
      假绿（已修复）", "修复前", "待议" all describe a tree that no longer exists,
      and a reader has to reconstruct which half of the paragraph is current.
      The test is whether the sentence tells you what to do now.

  R3  A claim that a file was deleted is checkable: either the file is gone or
      the sentence is false. Nobody checks these by hand, which is why a doc
      index once claimed five documents were deleted while two of them sat in
      the directory.

Exit codes: 0 clean, 1 findings, 2 nothing parsed. The third matters: every rule
here is a regex, and a regex that stops matching reports success forever.

Exclusion: docs/history-filtering-log.md is an append-only record. Its tables cite
past commits and paths on purpose -- correcting them would falsify the record, and
R1/R2 would flag every row. That reason cannot live in the file being scanned, so
it lives here.
"""
import io
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

EXCLUDE = {"history-filtering-log.md"}

SKIP_DIRS = {"bin", "obj", "dist", "libs", "node_modules", ".git", "_work",
             "packages"}   # src/packages is a NuGet restore target, not project docs


def scan_paths():
    """docs/*.md, root README.md, src/**/README.md, data/docs/*.md."""
    out = []
    docs = os.path.join(REPO, "docs")
    if os.path.isdir(docs):
        for name in sorted(os.listdir(docs)):
            if name.endswith(".md") and name not in EXCLUDE:
                out.append("docs/" + name)

    root_readme = os.path.join(REPO, "README.md")
    if os.path.isfile(root_readme):
        out.append("README.md")

    for sub in ("src", os.path.join("data", "docs")):
        base = os.path.join(REPO, sub)
        for root, dirs, files in os.walk(base):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for name in sorted(files):
                if name.endswith(".md"):
                    rel = os.path.relpath(os.path.join(root, name), REPO)
                    out.append(rel.replace(os.sep, "/"))

    return sorted(set(out))


# --- R1: a commit hash must not appear in prose -----------------------------
R1 = re.compile(r"(?:基线|HEAD)\s*`?\b[0-9a-f]{7}\b")


# --- R2: historical narration ----------------------------------------------
R2_WORDS = (
    "历史快照", "已被取代", "尚未执行", "TODO", "待办", "下一步",
    "待议", "留作后续",
)
# "曾经…" is only a defect when it narrates a fix ("曾经是假绿（已修复）");
# "曾经" describing a live constraint is fine.
R2_PATTERNS = (
    re.compile(r"曾经[^。\n]{0,40}已修复"),
    re.compile(r"修复前"),
)


# --- R3: deletion claims ---------------------------------------------------
DELETED = re.compile(r"(?:已删除|已移除|已移出)")
# Any token that looks like a path or filename with one of the extensions docs cite.
TOKEN = re.compile(r"[A-Za-z0-9_\-./\\]+\.(?:md|cs|xaml|py|ps1|lua|idl|json|dll|exe)")
# A row that already reports the fixed state is not a claim about the present.
HISTORICAL = ("已作废", "已修复", "已解决", "✅")

# The checkers that police these docs. A sentence naming one of them is describing
# the tool, not claiming it was deleted.
CHECKERS = {
    "check-doc-freshness.py",
    "check-doc-ranges.py",
    "check-doc-references.py",
    "report-doc-numbers.py",
    "report-missing-options.py",
}



def nearest_subject(line, verb_at, verb_end):
    """The file token a deletion verb governs, or None.

    Both orders occur: "`X` 已删除" (name then verb) and "已移除随附的 `X`" (verb then
    name). Forward wins, because a verb followed by a filename within one clause is
    naming its object -- in "已移除随附的 `menu.dll` 二进制…`dyn_menu.lua`" the backward
    token belongs to an earlier clause and is alive.
    """
    best, best_dist = None, 10 ** 9
    for m in TOKEN.finditer(line):
        if m.start() >= verb_end:
            dist = m.start() - verb_end
            if dist <= 40:
                return m.group(0)
            break
    for m in TOKEN.finditer(line):
        if m.end() <= verb_at:
            dist = verb_at - m.end()
            if dist < best_dist:
                best, best_dist = m.group(0), dist
    return best if best is not None and best_dist <= 40 else None


def blocks(lines):
    """Yield (text, first_line) for each paragraph or table/list run.

    A heading is its own block plus the paragraph under it, because a document index
    puts the claim in the heading and the names in the body.
    """
    i = 0
    n = len(lines)
    while i < n:
        if not lines[i].strip():
            i += 1
            continue
        start = i
        kind = ""
        if lines[i].lstrip().startswith("#"):
            kind = "heading"
        elif lines[i].lstrip().startswith("|"):
            kind = "table"
        elif lines[i].lstrip().startswith(("-", "*", ">")):
            kind = "list"

        if kind == "heading":
            i += 1
            while i < n and not lines[i].strip():
                i += 1
            while i < n and lines[i].strip() and not lines[i].lstrip().startswith("#"):
                i += 1
        elif kind == "table":
            while i < n and lines[i].lstrip().startswith("|"):
                i += 1
        elif kind == "list":
            while i < n and lines[i].strip():
                i += 1
        else:
            while i < n and lines[i].strip() and not lines[i].lstrip().startswith(
                    ("#", "|")):
                i += 1
        yield "\n".join(lines[start:i]), start + 1


def block_tokens(block, verb):
    """The file tokens a deletion verb in this block governs.

    Three shapes, all produced by this repo:

      "`X` 已删除"                 verb next to its object -> that object only
      "已移除随附的 `X`"           verb before its object -> forward match wins, so a
                                   live filename earlier in the clause is not indicted
      "## 旧文档已删除" + a list    verb in a heading that covers SEVERAL filenames ->
                                   all of them. This is the shape that kept two live
                                   documents behind an "已删除" heading for a release.

    A heading covering exactly one filename falls back to the nearest-subject rule:
    one name in the paragraph is prose, not a list of the deleted.
    """
    found = [t for t in TOKEN.findall(block) if os.path.basename(t) not in CHECKERS]
    head = block.split("\n")[0]
    if head.lstrip().startswith("#") and len(found) >= 2:
        return found
    subject = nearest_subject(block, verb.start(), verb.end())
    if subject is not None:
        return [subject]
    return found

def resolve(rel):
    """Repo-relative path, else a basename search -- same rule as check-doc-references."""
    direct = os.path.join(REPO, rel.replace("/", os.sep))
    if os.path.exists(direct):
        return "ok"
    base = os.path.basename(rel.replace("\\", "/"))
    for root, dirs, files in os.walk(REPO):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        if base in files:
            return "moved"
    return "missing"


def main():
    paths = scan_paths()
    if not paths:
        print("FAIL: no documents found -- the scan paths are wrong, not the docs")
        return 2

    findings = []
    scanned = 0

    for rel in paths:
        path = os.path.join(REPO, rel.replace("/", os.sep))
        try:
            text = io.open(path, encoding="utf-8-sig").read()
        except OSError as exc:
            print("FAIL: cannot read %s (%s)" % (rel, exc))
            return 2
        scanned += 1

        lines = text.split("\n")

        for lineno, line in enumerate(lines, 1):
            m = R1.search(line)
            if m:
                findings.append((rel, lineno, "R1", "prose states a commit hash: %r"
                                 % m.group(0).strip()))

            for word in R2_WORDS:
                if word in line:
                    findings.append((rel, lineno, "R2",
                                     "historical/pending wording %r -- say what is true now"
                                     % word))
                    break
            else:
                for pat in R2_PATTERNS:
                    m2 = pat.search(line)
                    if m2:
                        findings.append((rel, lineno, "R2",
                                         "narrates a fix (%r) -- state the rule, not its history"
                                         % m2.group(0)))
                        break

        # R3 works on blocks, not lines: the claim and the filenames it covers are
        # routinely one paragraph or one list item apart --
        #   "本套文档替换了 5 份旧文档（settings-design-contract.md、...）"
        # with "旧文档已删除" as the heading above it.
        for block, first_line in blocks(lines):
            verb = DELETED.search(block)
            if not verb:
                continue
            if any(tag in block for tag in HISTORICAL):
                continue
            for token in dict.fromkeys(block_tokens(block, verb)):
                state = resolve(token)
                if state != "missing":
                    findings.append((
                        rel, first_line, "R3",
                        "claims %s is deleted, but it %s"
                        % (token, "exists" if state == "ok" else "exists elsewhere")))

    print("documents scanned: %d" % scanned)
    for rel, lineno, rule, msg in findings:
        print("FAIL: %-34s %s:%d  %s" % (rule, rel, lineno, msg))

    if findings:
        print()
        print("FAIL: %d finding(s). R1: no hashes in prose. R2: no history under a live")
        print("heading. R3: a deletion claim must match the tree.")
        return 1

    print("OK: %d document(s) describe the current tree" % scanned)
    return 0


if __name__ == "__main__":
    sys.exit(main())
