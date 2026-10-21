#!/usr/bin/env python3
"""Check the settings section catalog against AppLang.

A settings section used to be identified by its localized caption. Three
places worked around that independently: SectionMeta() compared the caption
against 51 hardcoded branches for an icon and a description, this project's
layout code reflected over the AppLang property names to derive a stable id,
and the persisted layout stored that id. SettingsSections now holds all of it
in one table, so the caption, the icon and the description cannot drift apart.

That only holds if the table and AppLang agree, which is what this checks:

  * every Section* property in AppLang (excluding the SectionDesc* ones) has
    exactly one row in the table, and no row names a property that is gone;
  * every row's description accessor is the "SectionDesc" property that
    belongs to its section;
  * ids are unique;
  * every SettingsCategory* property has a row, and vice versa;
  * every language file carries a real translation for every section and
    category caption -- see below;
  * every option the settings tree builds has a sectionMap entry, which is the
    same condition BuildSettings() throws on. A missing entry kills the app the
    moment someone opens Settings, so this is checked here rather than
    discovered by a user;
  * repeated option sort keys are surfaced as warnings, because optionOrder is
    deliberately both an ordering key and a grouping label.

Icon collisions are reported as warnings rather than errors: a section card is
scanned by its glyph, so two sections sharing one is a real readability
problem, but it is a design choice rather than a broken invariant. Three pairs
shared a glyph when the table was introduced.

Why the translations are checked here and not in check-localization.py: that
script asserts every AppLang property has a key in every file, which a key
whose value is still the English source satisfies. Section captions are the
one family where "translated" is not optional -- they are the tree's
navigation, and the table above is useless if the caption rendered next to the
icon is in the wrong language. Residual-English is reported as a warning (a
caption that is genuinely the same word in two languages is not a defect), a
missing key as an error.

The residual-English warnings currently emitted are all of the harmless kind
and should stay: "Direct3D 11" is a product name, "HTTP" is a protocol, and
"OSD" is left untranslated in Chinese, Japanese and Korean interfaces too. Do
not "fix" those by inventing translations.

Exit code 1 on errors.
"""

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "mpv-winui" / "mpv-winui"

APPLANG = SRC / "Modules" / "Language" / "AppLang.cs"
CATALOG = SRC / "Modules" / "Settings" / "Layout" / "SettingsSections.cs"
LANGUAGES = SRC / "Languages"
OPTIONS = (SRC / "Modules" / "Settings" / "SettingsPage.Options.cs",
           SRC / "Modules" / "Settings" / "SettingsPage.Options.Program.cs")
OPTION_FILES = tuple(sorted((SRC / "Modules" / "Settings").glob("SettingsPage.Options*.cs")))
CORE_OPTIONS = SRC / "Modules" / "Settings" / "SettingsPage.Options.cs"
GEN_SECTIONS = SRC / "Modules" / "Settings" / "SettingsPage.Sections.Gen.cs"


def applang_props():
    """Section and category property names AppLang declares.

    Read from the hand-written AppLang.cs plus the generated partials. The
    generated half is where the schema sections' captions live, and it is
    still AppLang: a check that only read AppLang.cs would call every
    generated section undeclared.
    """
    text = APPLANG.read_text(encoding="utf-8-sig")
    for gen in sorted(APPLANG.parent.glob("AppLang*.Gen.cs")):
        text += "\n" + gen.read_text(encoding="utf-8-sig")
    names = re.findall(r'public string ([A-Za-z0-9_]+) \{ get; set; \}', text)
    sections = {n for n in names
                if n.startswith("Section") and not n.startswith("SectionDesc")}
    descs = {n for n in names if n.startswith("SectionDesc")}
    categories = {n for n in names if n.startswith("SettingsCategory")}
    return sections, descs, categories


def category_values():
    """{property name: default caption} for every SettingsCategory* property.

    A property is a *source* of a caption, and since the six-domain restructure
    several resolve to the same one on purpose (Network and Shortcuts both feed
    Playback and Interface; they were mpv-manual chapters, not domains). So the
    checker has to compare values, not property names.
    """
    text = APPLANG.read_text(encoding="utf-8-sig")
    return dict(re.findall(
        r'public string (SettingsCategory[A-Za-z0-9]+) \{ get; set; \} = "([^"]*)"',
        text))


def catalog_rows():
    text = CATALOG.read_text(encoding="utf-8-sig")
    rows = re.findall(
        r'new\("(Section[A-Za-z0-9]+)",\s*"(\\u[0-9A-Fa-f]{4})",'
        r'\s*l => l\.(Section[A-Za-z0-9]+),\s*l => l\.(SectionDesc[A-Za-z0-9]+)\)',
        text)
    cats = re.findall(
        r'new\("([a-z]+)",\s*l => l\.(SettingsCategory[A-Za-z0-9]+)\)', text)
    return rows, cats


def caption_translations():
    """Load every language file as {filename: parsed-json-or-exception}.

    Parsing failures are captured rather than raised so the caller can report
    them alongside the other problems instead of dying on the first bad file.
    """
    files = sorted(LANGUAGES.glob("*.json"))
    loaded = {}
    for path in files:
        try:
            loaded[path.name] = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as exc:
            loaded[path.name] = exc
    return loaded


def option_sort_keys():
    """The [nameof(AppSettings.X)] = N entries from every options file.

    Returned as {value: [names]}, because repeats are meaningful rather than
    accidental -- see the sort-key section of main().
    """
    keys = {}
    for path in OPTIONS:
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8-sig")
        for name, value in re.findall(
                r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*(\d+),', text):
            keys.setdefault(int(value), []).append(name)
    return keys


# Option keys the settings tree builds at runtime instead of declaring in a
# static row. BuildSettings() exempts them from the sectionMap guard (they carry
# their own inline Section), so a check that mirrors that guard has to exempt
# them too -- and by prefix, because the suffixes are per-user data:
#   Shortcut:       built from input.conf bindings
#   custom-section: one placeholder row per folder the user created
RUNTIME_KEY_PREFIXES = ("Shortcut:", "custom-section:")


def option_blocks(text):
    """The body of every `new Option { ... }` initializer in one source text.

    Brace matching, not a line regex. The regex this replaced required the
    closing `},` to start its own line, which the generator's output never does:
    SettingsPage.Options.Gen.cs closes all 362 of its blocks with `});` at the
    end of the last property line, so the pattern matched ZERO of them. The
    option keys therefore came out 322 short, and every generated key was then
    reported by the reverse check below as a sectionMap entry "naming no built
    option" -- 362 false warnings that buried the one genuine stale key
    (ControlBarHiddenIcons, since removed).

    Matching braces cannot be fooled by how the block is closed. It also stops
    at the initializer's own brace rather than at the first `},`, so a nested
    object initializer inside an Option would not truncate the block.
    """
    blocks = []
    start = 0
    while True:
        i = text.find("new Option", start)
        if i < 0:
            return blocks
        j = text.find("{", i)
        if j < 0:
            return blocks
        depth = 0
        k = j
        while k < len(text):
            if text[k] == "{":
                depth += 1
            elif text[k] == "}":
                depth -= 1
                if depth == 0:
                    break
            k += 1
        blocks.append(text[j:k + 1])
        start = k + 1


def option_keys():
    """Every option key the settings tree actually builds.

    This mirrors BuildSettings()'s own scan. Keys come in three spellings --
    nameof(AppContext.AppSetting.X), nameof(AppSettings.X), and a plain string
    (or a string constant, as FileAssociationCheckList uses) -- and all three
    are real options, so all three have to be recognised here. An earlier
    version of this script only understood nameof(...) and reported two live
    options as orphans.

    The hand-written half and the generated half both have to be read: the 362
    schema rows are real rows on the same list, and a check that knew only about
    the hand-written half would report all 362 of their sectionMap entries as
    dead. See option_blocks() for the failure that actually happened here.

    Shortcut:N keys are generated at runtime from input.conf and have no
    static row, so they never appear in a source file.
    """
    consts = {}
    # Constants can live outside the options files -- FileAssociationCheckListKey
    # is declared in SettingsPage.Actions.cs -- so sweep the whole Settings
    # folder rather than just the files that build options.
    for path in sorted((SRC / "Modules" / "Settings").glob("*.cs")):
        for name, value in re.findall(
                r'const\s+string\s+(\w+)\s*=\s*"([^"]+)"',
                path.read_text(encoding="utf-8-sig")):
            consts[name] = value

    keys = set()
    for path in OPTION_FILES:
        text = path.read_text(encoding="utf-8-sig")
        for block in option_blocks(text):
            match = (re.search(r'Key\s*=\s*nameof\([A-Za-z_.]*\.(\w+)\)', block)
                     or re.search(r'Key\s*=\s*"([^"]+)"', block)
                     or re.search(r'Key\s*=\s*(\w+)\s*,', block))
            if not match:
                continue
            key = consts.get(match.group(1), match.group(1))
            # Runtime-generated keys are exempt from the sectionMap guard, and
            # the exemption has to be mirrored here or this checker reports a
            # crash that cannot happen. `custom-section:` is the second such
            # family: SettingsPage.Options.Layout.cs builds one placeholder row
            # per user-created folder, keyed by that folder's id, and the row
            # carries its own inline Section -- which is precisely the condition
            # BuildSettings() exempts. `Shortcut:` is the same idea, built from
            # input.conf. Both are exempted by prefix, never by name: the
            # suffixes do not exist until the user makes a folder or a binding.
            if key.startswith(RUNTIME_KEY_PREFIXES):
                continue
            keys.add(key)
    return keys, consts


def section_map_keys():
    """The keys of the sectionMap dictionary in SettingsPage.Options.cs.

    Brace matching rather than a regex to the end of file, because the map is
    followed by more code and an over-broad match would pick up optionOrder's
    keys as well.

    The schema-generated rows register through RegisterGeneratedSections()
    instead of appearing inline, so the literal scan below cannot see them.
    Their entries are read from SettingsPage.Sections.Gen.cs and unioned in:
    the two halves are one map at runtime, and a check that only knew about
    the hand-written half would report all 362 generated keys as unmapped.
    """
    text = CORE_OPTIONS.read_text(encoding="utf-8-sig")
    parts = text.split("var sectionMap = new Dictionary<string, string>", 1)
    if len(parts) < 2:
        return None
    body = parts[1]
    depth = 0
    end = None
    for i, ch in enumerate(body):
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                end = i
                break
    if end is None:
        return None
    inline = {a or b for a, b in re.findall(
        r'\[(?:nameof\([A-Za-z_.]*\.(\w+)\)|"([^"]+)")', body[:end])}
    return inline | generated_section_map_keys()


def generated_section_map_keys():
    """Keys RegisterGeneratedSections() writes into sectionMap.

    Absent (not an error) when the generator has not been run: a checkout
    without the generated files is a legitimate state, and the pages that
    need them do not build either.
    """
    if not GEN_SECTIONS.exists():
        return set()
    text = GEN_SECTIONS.read_text(encoding="utf-8-sig")
    return {m for m in re.findall(
        r'sectionMap\[nameof\([A-Za-z_.]*\.(\w+)\)\]', text)}


def generated_sections_used():
    """Section AppLang properties RegisterGeneratedSections() registers.

    Returned as property names (SectionGenAudio), not the local variables the
    hand-written half uses, because that is the form the catalog compares in.
    """
    if not GEN_SECTIONS.exists():
        return set()
    text = GEN_SECTIONS.read_text(encoding="utf-8-sig")
    return {m for m in re.findall(r'advancedSections\.Add\(lang\.(\w+)\)', text)}


def section_variables():
    """Local section variable -> the AppLang property it aliases.

    sectionMap is keyed by the local variable (sGpuScaling), while the catalog
    and AppLang use the property name (SectionGpuScaling). Both directions of the
    empty-section check need the translation, so it is built once here.

    Only the declarations in SettingsPage.Options.cs are read: that is where the
    variables are introduced, and a second declaration elsewhere would mean two
    different things under one name -- which this check should not paper over.
    """
    text = CORE_OPTIONS.read_text(encoding="utf-8-sig")
    return dict(re.findall(
        r'\bvar\s+(s[A-Za-z0-9]+)\s*=\s*AppContext\.AppLang\.(Section[A-Za-z0-9]+)\s*;',
        text))


def sections_used_by_options():
    """Every section variable sectionMap assigns an option to."""
    text = CORE_OPTIONS.read_text(encoding="utf-8-sig")
    parts = text.split("var sectionMap = new Dictionary<string, string>", 1)
    if len(parts) < 2:
        return None, None
    body = parts[1]
    depth = 0
    end = None
    for i, ch in enumerate(body):
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                end = i
                break
    if end is None:
        return None, None
    return set(re.findall(r'\]\s*=\s*(s[A-Za-z0-9]+)\s*,', body[:end])), body[:end]


def section_order_names():
    """AppLang property names declared in the sectionOrder dictionary.

    Keyed by local variable (sGpuScaling), so callers translate through
    section_variables() -- same two-step as sectionMap.
    """
    text = CORE_OPTIONS.read_text(encoding="utf-8-sig")
    parts = text.split("var sectionOrder = new Dictionary<string, int>", 1)
    if len(parts) < 2:
        return None
    body = parts[1]
    depth = 0
    end = None
    for i, ch in enumerate(body):
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                end = i
                break
    if end is None:
        return None
    return set(re.findall(r'\[(s[A-Za-z0-9]+)\]\s*=\s*\d+\s*,', body[:end]))


def main():
    sections, descs, categories = applang_props()
    rows, cats = catalog_rows()

    errors = []
    warnings = []

    ids = [r[0] for r in rows]
    for dup in sorted({i for i in ids if ids.count(i) > 1}):
        errors.append(f"duplicate section id: {dup}")

    row_names = {r[2] for r in rows}
    for name in sorted(sections - row_names):
        errors.append(f"AppLang.{name} has no row in SettingsSections")
    for name in sorted(row_names - sections):
        errors.append(f"SettingsSections names {name}, which AppLang does not declare")

    row_descs = {r[3] for r in rows}
    for name in sorted(descs - row_descs):
        errors.append(f"AppLang.{name} is never used as a description")
    for name in sorted(row_descs - descs):
        errors.append(f"SettingsSections uses {name}, which AppLang does not declare")

    for _, _, cap, desc in rows:
        expected = "SectionDesc" + cap[len("Section"):]
        if desc != expected:
            errors.append(f"{cap} is paired with {desc}, expected {expected}")

    # --- categories ---------------------------------------------------------
    # A SettingsCategory* property is a *source* of a caption, not necessarily a
    # category of its own. Since the six-domain restructure several resolve to
    # the same caption on purpose: Network and Shortcuts were mpv-manual
    # chapters, and what they hold belongs to Playback and Interface. So the
    # invariant is not "one row per property" -- it is:
    #
    #   * no row names a property AppLang does not declare
    #   * no two rows share a key (they would collide in the rail)
    #   * every declared property resolves to a caption some row actually shows
    #
    # The last one is the useful half: a property whose caption no row shows is
    # a category the user can never reach.
    cat_names = {c[1] for c in cats}
    for name in sorted(cat_names - categories):
        errors.append(f"SettingsSections.Categories names {name}, which AppLang does not declare")

    keys = [c[0] for c in cats]
    for dup in sorted({k for k in keys if keys.count(k) > 1}):
        errors.append(f"duplicate category key: {dup}")

    values = category_values()
    shown = {values[c[1]] for c in cats if c[1] in values}
    for name in sorted(categories):
        value = values.get(name)
        if value is not None and value not in shown:
            errors.append(
                f"AppLang.{name} resolves to {value!r}, which no "
                f"SettingsSections.Categories row shows -- that category is "
                f"unreachable")

    # --- caption translations ------------------------------------------------
    # Every section caption, every section description and every category
    # caption must exist in every language file. Values equal to the en-US text
    # are a warning, not an error.
    caption_props = sorted(row_names | row_descs | cat_names)
    files = caption_translations()
    if not files:
        errors.append(f"no language files found under {LANGUAGES}")
    baseline = files.get("en-US.json", {})
    if isinstance(baseline, Exception):
        errors.append(f"en-US.json is not valid JSON: {baseline}")
        baseline = {}
    for name, data in sorted(files.items()):
        if isinstance(data, Exception):
            errors.append(f"{name} is not valid JSON: {data}")
            continue
        missing = [p for p in caption_props if p not in data]
        if missing:
            errors.append(f"{name} is missing {len(missing)} caption key(s): "
                          f"{', '.join(missing[:5])}"
                          + (" ..." if len(missing) > 5 else ""))
        if name == "en-US.json":
            continue
        untranslated = [p for p in caption_props
                        if p in data and data[p] == baseline.get(p)]
        if untranslated:
            warnings.append(f"{name}: {len(untranslated)} caption(s) still in English: "
                            f"{', '.join(untranslated[:5])}"
                            + (" ..." if len(untranslated) > 5 else ""))

    # --- BuildSettings()'s own guards, mirrored ------------------------------
    # The app refuses to open the settings window when either of these is
    # unsatisfied: BuildSettings() throws InvalidOperationException, which
    # surfaces as a stowed exception in Microsoft.UI.Xaml.dll and kills the
    # process -- no managed stack, no log line, just "clicking Settings closes
    # the app". That happened for real: rebuilding the subtitles map dropped
    # SubHdrPeak while the option itself stayed, and the only symptom was a
    # crash on click. The runtime guard is correct; this is the same guard run
    # at commit time so the crash never reaches a user.
    keys, consts = option_keys()
    mapped = section_map_keys()
    if mapped is None:
        errors.append("could not find sectionMap in SettingsPage.Options.cs")
    else:
        unmapped = sorted(k for k in keys if k not in mapped)
        if unmapped:
            errors.append(
                f"{len(unmapped)} option(s) have no sectionMap entry, which makes "
                f"BuildSettings() throw on open: {', '.join(unmapped[:6])}"
                + (" ..." if len(unmapped) > 6 else ""))
        # The reverse is not a failure: a map entry for an option that no longer
        # exists is dead weight, but it never breaks the tree. Reported as a
        # warning so the stale entries get cleaned up rather than accumulating.
        stale = sorted(k for k in mapped if k not in keys)
        if stale:
            warnings.append(
                f"{len(stale)} sectionMap entry/entries name no built option: "
                f"{', '.join(stale[:6])}" + (" ..." if len(stale) > 6 else ""))

    # --- sections that no option belongs to ----------------------------------
    # A section is only rendered when an option claims it, so a declared-but-empty
    # section is invisible: it sits in the catalog, the language files and the
    # icon table, and nothing ever shows it. That is how a section outlives its
    # options when they are moved to a better home, and it is the same failure
    # shape as the crash this script already guards against -- the declaration and
    # the content drifting apart, with the mismatch silent either way.
    #
    # Reported as an error rather than a warning because a stale section is never
    # intentional: it means the last edit that emptied it forgot to remove it.
    #
    # Measured against the catalog's own rows, NOT against AppLang: a section
    # emptied by an edit is usually stripped from AppLang in the same commit, and
    # keying this off AppLang would make the check go quiet exactly when it is
    # needed. (Verified by injecting a stale row back into the catalog.)
    variables = section_variables()
    used, _ = sections_used_by_options()
    if variables and used is not None:
        used_props = {variables.get(v) for v in used} - {None}
        # The generated registration names its sections by AppLang property
        # directly (lang.SectionGenAudio), with no local variable in between,
        # so they join the used set as-is.
        used_props |= generated_sections_used()
        for name in sorted({r[2] for r in rows} - used_props):
            errors.append(
                f"{name} is declared in SettingsSections but no option maps to it, "
                f"so it can never be shown -- remove the declaration, or point the "
                f"options that belong there at it")

        # The other direction: sectionMap naming a variable that is not a section.
        for var in sorted(v for v in used if v not in variables):
            errors.append(
                f"sectionMap assigns options to {var}, which is not a declared "
                f"section variable in {CORE_OPTIONS.name}")

    # --- every used section must have an order key ---------------------------
    # A section a row maps to, but which sectionOrder never mentions, does not
    # fail and does not warn: BuildSettings falls back to int.MaxValue, so the
    # section sorts to the BOTTOM of its category. Three sections were in that
    # state -- sVideoGeometry, sOsdText and sWindowBehavior, 18 rows between
    # them -- and sOsdText was the worst of the three, because it landed past
    # Metadata on the far side of the very sections it supplies the font metrics
    # for. Nothing else in this script looked at sectionOrder at all.
    #
    # It is an error rather than a warning because "no order" is never a
    # decision: sectionOrder is per-category (options group by category first),
    # so a section without a key has no defined place among its siblings and the
    # resulting order is an accident of a missing line.
    order_vars = section_order_names()
    if order_vars is not None and variables is not None and used is not None:
        # Recompute rather than reuse used_props from the block above: that one is
        # scoped to its own conditional, and depending on it would make this check
        # silently skip whenever the earlier guard did not run.
        used_for_order = {variables.get(v) for v in used} - {None}
        used_for_order |= generated_sections_used()
        ordered_props = {variables.get(v) for v in order_vars} - {None}
        # Generated sections register their order in their own file, by AppLang
        # property name; same union as the empty-section check above.
        ordered_props |= generated_sections_used()
        for name in sorted(used_for_order - ordered_props):
            errors.append(
                f"{name} is used by sectionMap but has no sectionOrder entry, so it "
                f"silently sorts to the end of its category -- add it to sectionOrder")

    # --- sort keys -----------------------------------------------------------
    # A repeated sort key is NOT an error here. optionOrder is deliberately
    # overloaded: it is both the ordering key and a grouping label. The PiP
    # controls share 193 on purpose (WindowPiP, WindowPiPShowControls,
    # WindowPiPShowTopButtons), and the Window block shares 194-196 the same
    # way. LINQ's OrderBy is stable, so equal keys keep their source order --
    # which is exactly the intended "these belong together, in written order".
    #
    # The real hazard is different: a duplicate makes the on-screen order
    # depend on where the lines physically sit in the file, so reordering the
    # file silently reorders the UI. That is worth a warning, not a failure.
    sort_keys = option_sort_keys()
    for value, names in sorted(sort_keys.items()):
        if len(names) > 1:
            warnings.append(f"sort key {value} is shared by {len(names)} options "
                            f"({', '.join(sorted(names)[:4])}) -- they keep source "
                            f"order, so moving those lines reorders the UI")

    by_icon = {}
    for _, icon, cap, _ in rows:
        by_icon.setdefault(icon, []).append(cap)
    for icon, caps in sorted(by_icon.items()):
        if len(caps) > 1:
            warnings.append(f"{icon} is shared by {', '.join(sorted(caps))}")

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1

    shared = sum(1 for caps in by_icon.values() if len(caps) > 1)
    print(f"OK: {len(rows)} sections, {len(cats)} categories, "
          f"{len(by_icon)} distinct icons, {shared} shared, "
          f"{len(sort_keys)} sort keys, {len(files)} language files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
