"""Which x:Key resources are actually read, counting every way the app reads them.

The previous inventory in this repo counted `{StaticResource X}` and `{ThemeResource X}`
and called the rest orphans. It then found `MpvChapterTickBrush` being read by
`Resources.TryGetValue("MpvChapterTickBrush", ...)` -- a string lookup that no XAML scan
sees -- and had to retract. That is the failure mode to avoid: a token nothing reads is
safe to delete, but "my regex found no reader" is not the same as "nothing reads it".

So this counts four readers, and only reports a key orphan when all four are silent:

  1. {StaticResource X} / {ThemeResource X} in any .xaml
  2. Resources.TryGetValue("X", ...) / Resources["X"] in any .cs
  3. FindName("X") / GetTemplateChild("X")
  4. the key's NAME appearing as a bare string literal anywhere (broadest net, and the
     one that catches the case above generically rather than by special-casing it)

Reported with the count for each, so a key with a hit in category 4 only can be judged
by a human instead of being deleted on a script's word.
"""
import io
import os
import re
import sys
from collections import defaultdict

APP = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "mpv-winui", "mpv-winui")
KEY = re.compile(r'x:Key="([A-Za-z0-9_.]+)"')


def source_files(ext):
    for root, dirs, files in os.walk(APP):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for f in files:
            if f.endswith(ext):
                yield os.path.join(root, f)


def main():
    keys = {}
    for path in source_files(".xaml"):
        text = io.open(path, encoding="utf-8-sig", errors="replace").read()
        for m in KEY.finditer(text):
            keys[m.group(1)] = os.path.basename(path)
    print("defined x:Key resources:", len(keys))

    corpus = []
    for ext in (".xaml", ".cs"):
        for path in source_files(ext):
            corpus.append((os.path.basename(path),
                           io.open(path, encoding="utf-8-sig",
                                   errors="replace").read()))

    counts = defaultdict(lambda: defaultdict(int))
    for name, text in corpus:
        for key in keys:
            counts[key]["xaml_ref"] += len(
                re.findall(r"\{(?:Static|Theme)Resource\s+%s\s*\}" % re.escape(key), text))
            counts[key]["code_ref"] += len(
                re.findall(r'TryGetValue\(\s*"%s"' % re.escape(key), text)) + \
                len(re.findall(r'Resources\[\s*"%s"\s*\]' % re.escape(key), text))
            counts[key]["find_name"] += len(
                re.findall(r'(?:FindName|GetTemplateChild)\(\s*"%s"' % re.escape(key), text))
            counts[key]["literal"] += len(
                re.findall(r'"%s"' % re.escape(key), text))

    total = lambda k: sum(counts[k].values())
    orphans = sorted(k for k in keys if total(k) == 0)
    literal_only = sorted(k for k in keys
                          if total(k) > 0 and counts[k]["xaml_ref"] == 0
                          and counts[k]["code_ref"] == 0 and counts[k]["find_name"] == 0)

    print("with NO reader at all:", len(orphans))
    for k in orphans:
        print("   %-40s defined in %s" % (k, keys[k]))
    print()
    print("read ONLY as a bare string literal:", len(literal_only))
    for k in literal_only[:12]:
        print("   %-40s %s" % (k, dict(counts[k])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
