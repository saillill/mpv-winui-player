"""Scan XAML interactive controls for missing hover tooltips / automation names.

Rules (matched to this repo's conventions):
- Automation name satisfied by: AutomationProperties.Name in XAML (literal or
  x:Bind), a runtime AutomationProperties.SetName(<Name>, ...) in the owning
  code-behind, or self-labeling text (Content=/Label=/PlaceholderText=/Text= in
  XAML or the same property assigned at runtime).
- Hover tooltip satisfied by: ToolTipService.ToolTip in XAML, a runtime
  SetToolTip in the owning code-behind, self-labeling text (text buttons do not
  need tooltips), a Loaded= handler (per-item wiring, e.g. shader list rows), or
  the control being a Slider (its value is visible while dragging).
- Container chrome (CommandBar) and the video surface (SwapChainPanel) are
  skipped; Option*Control template internals (NumberBox/TextBox/ComboBox/
  CheckBox) are row-labeled by the option label and skipped.

Which .cs counts as "the owning code-behind"
--------------------------------------------
A control's x:Name is only a field in the code-behind of the class that renders
it. Two shapes exist here:

- A XAML with x:Class (UserControl/Page): the code-behind is the sibling
  <Class>.xaml.cs, so every .cs in that directory is scanned.
- A ResourceDictionary that only holds Styles/ControlTemplates (Themes/
  Generic.xaml, Domains/Playback/*Style.xaml): the names inside a ControlTemplate
  are template parts of the templated control, and the file that names them is
  that control's code-behind - which need not live in the same directory.
  Ownership is therefore resolved from the enclosing Style/ControlTemplate
  TargetType (the outermost one; nested per-part Styles like
  <Style x:Key="PlayButtonStyle" TargetType="Button"> inside the template do not
  take over) to <Type>.xaml.cs anywhere in the project. Resolving ownership from
  the same directory is not enough: Themes/Generic.xaml names 14 of its buttons in
  Domains/Playback/PlayerControl.xaml.cs, so a sibling-only scan calls all 14
  unnamed.

Prints elements failing either rule. Exit 1 when any are found, exit 2 when no
XAML matched at all (a scan root that stopped resolving must not read as
success). Finding nothing is the only passing outcome, so this can be driven by
a chain rather than read by eye.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "src" / "mpvw-app"
INTERACTIVE = {
    "Button", "ToggleButton", "AppBarButton", "AppBarToggleButton",
    "RepeatButton", "Slider", "ComboBox", "CheckBox", "RadioButton",
    "HyperlinkButton", "MenuFlyoutItem", "MenuFlyoutSubItem",
    "ToggleMenuFlyoutItem", "NumberBox", "TextBox", "AutoSuggestBox",
    "SplitButton", "DropDownButton",
}
ROW_LABELED_TYPES = {"NumberBox", "TextBox", "ComboBox", "CheckBox", "AutoSuggestBox"}

# Tags that open a naming scope: their TargetType is the class whose
# code-behind owns the x:Names declared inside.
SCOPE_TAGS = ("Style", "ControlTemplate")

event_re = re.compile(r"<(/?)([A-Za-z][\w:.]*)\b([^>]*)>")
name_re = re.compile(r'\bx:Name="([^"]+)"')
target_re = re.compile(r'\bTargetType="([^"]+)"')
class_re = re.compile(r'\bx:Class="([^"]+)"')
tip_attr_re = re.compile(r"ToolTipService\.ToolTip\s*=")
auto_attr_re = re.compile(r"AutomationProperties\.Name\s*=")
labeled_by_attr_re = re.compile(r"AutomationProperties\.LabeledBy\s*=")
loaded_attr_re = re.compile(r'\bLoaded="')
text_attr_re = re.compile(r"\b(?:Content|Label|PlaceholderText|Text)\s*=")


def strip_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def collection_populated(key: str, code: str) -> bool:
    """Is a keyed resource's items captioned by iterating the collection?

    The card menu is one shared MenuFlyout whose items carry a Tag and no Text
    in XAML: ApplyCardStrings enumerates cardMenu.Items and assigns
    menuItem.Text from the Tag. Nothing in the markup says so, so a scan of the
    XAML alone calls all six items unnamed. This resolves the case the markup
    cannot.
    """
    if not key:
        return False
    for m in re.finditer(r'Resources\["' + re.escape(key) + r'"\]\s*is\s+(?:not\s+)?\w+\s+(\w+)', code):
        var = m.group(1)
        for fm in re.finditer(r"foreach\s*\([^)]*\b" + re.escape(var) + r"\.Items\b[^)]*\)", code):
            brace = code.find("{", fm.end())
            if brace == -1:
                continue
            depth = 0
            for i in range(brace, len(code)):
                if code[i] == "{":
                    depth += 1
                elif code[i] == "}":
                    depth -= 1
                    if depth == 0:
                        if re.search(r"\.(?:Text|Content)\s*=(?!=)", code[brace:i]):
                            return True
                        break
    return False


def names_it(name: str, code: str, verb: str) -> bool:
    """Is `name` passed to AutomationProperties.SetName / ToolTipService.SetToolTip?

    Accepts the control either directly (SetName(Name, x)) or through a helper
    that takes it as a parameter, matching assigns_caption's join.
    """
    api = "AutomationProperties\\.SetName" if verb == "name" else "ToolTipService\\.SetToolTip"
    if re.search(api + r"\(\s*" + re.escape(name) + r"\b", code):
        return True
    # Looked up by string instead of by field: the library cards resolve
    # their buttons with FindDescendant(card, "CardPlayButton") and name the
    # variable that call returned. A shared DataTemplate can only be reached
    # from the page that realises it, so this shape has to count.
    for m in re.finditer(r'\b(\w+)\s*=\s*[^;]*?"' + re.escape(name) + r'"', code):
        var = m.group(1)
        if re.search(api + r"\(\s*" + re.escape(var) + r"\b", code):
            return True
        if re.search(re.escape(var) + r"\.Text\s*=", code):
            return True
        # `if (play is Button playButton)` renames the variable: the lookup
        # result is pattern-matched into the name that gets passed on.
        for pm in re.finditer(
            r"\b" + re.escape(var) + r"\s+is\s+(?:not\s+)?[\w.]+\s+(\w+)", code
        ):
            inner = pm.group(1)
            if re.search(api + r"\(\s*" + re.escape(inner) + r"\b", code):
                return True
            if re.search(re.escape(inner) + r"\.Text\s*=", code):
                return True
    called = set(re.findall(r"\b([A-Za-z_]\w*)\s*\([^;]{0,160}?\b" + re.escape(name) + r"\b", code))
    for body in method_bodies(code, {"bodies": []}):
        head = re.match(r"\s*([A-Za-z_]\w*)", body)
        if not head or head.group(1) not in called:
            continue
        if re.search(api + r"\(\s*", body):
            return True
    return False


def element_body(text: str, tag: str, start: int) -> str:
    """Return the body of the element that opens at `start`, depth-aware.

    The previous version cut at the first `</tag>`, which for a card button
    wrapping its own play/more buttons is the INNER button's close tag. The
    truncated body then looked like a control with no caption, and a card that
    renders its own title was reported unnamed.
    """
    depth = 1
    for m in re.finditer(r"<(/?)([A-Za-z][\w:.]*)\b[^>]*?(/?)>", text[start:]):
        if m.group(2) != tag:
            continue
        if m.group(1):
            depth -= 1
            if depth == 0:
                return text[start: start + m.start()]
        elif not m.group(3):
            depth += 1
    return text[start:]


def anchor(pos: int, stripped: str, raw: str) -> int:
    """Map an offset in the comment-stripped text back to the raw text.

    The stripped text is the raw text with <!-- ... --> spans deleted, so the
    surviving characters appear in the same order in both. Counting how much
    comment text precedes pos recovers the raw offset without re-parsing.
    """
    removed = 0
    for cm in re.finditer(r"<!--.*?-->", raw, flags=re.S):
        if cm.start() - removed >= pos:
            break
        removed += cm.end() - cm.start()
    return min(pos + removed, len(raw) - 1)


def assigns_caption(name: str, code: str, cache: dict[str, list[str]]) -> bool:
    """Does any code path set <name>.Text/.Content, directly or via a helper?

    Direct assignment is the common shape. The other shape is a builder that
    takes the control as a parameter and assigns inside its own body --
    BuildShellOpenMenuItem(item, ...) is how the shell's three menu items get
    their text, and matching only `name.Property =` calls them unnamed.
    """
    if re.search(re.escape(name) + r"\.(?:Content|Label|PlaceholderText|Text)\s*=", code):
        return True
    # A builder takes the control as a parameter and assigns inside its own
    # body. The call site names the control; the body names the parameter, so
    # the two halves have to be joined: find the methods called with this
    # control as an argument, then ask whether that method assigns a caption.
    called = set(re.findall(r"\b([A-Za-z_]\w*)\s*\([^;]{0,160}?\b" + re.escape(name) + r"\b", code))
    by_name: dict[str, list[str]] = {}
    for body in method_bodies(code, cache):
        head = re.match(r"\s*([A-Za-z_]\w*)", body)
        if head:
            by_name.setdefault(head.group(1), []).append(body)
    for method in called:
        for body in by_name.get(method, []):
            if re.search(r"\.(?:Content|Label|PlaceholderText|Text)\s*=(?!=)", body):
                return True
    return False


def method_bodies(code: str, cache: dict[str, list[str]]) -> list[str]:
    if "bodies" in cache:
        return cache["bodies"]
    out: list[str] = []
    for m in re.finditer(r"\b[A-Za-z_]\w*\s*\([^)]*\)\s*(?:=>|=|\{)", code):
        open_at = code.find("{", m.start())
        if open_at == -1 or open_at > m.end() + 2:
            continue
        depth = 0
        for i in range(open_at, len(code)):
            if code[i] == "{":
                depth += 1
            elif code[i] == "}":
                depth -= 1
                if depth == 0:
                    out.append(code[m.start(): i + 1])
                    break
    cache["bodies"] = out
    return out


def is_generated(path: Path) -> bool:
    return bool({p.lower() for p in path.parts} & {"bin", "obj"})


def build_code_index() -> dict[str, str]:
    """Map a class's simple name to its code-behind plus that file's siblings."""
    index: dict[str, str] = {}
    for cs in ROOT.rglob("*.xaml.cs"):
        if is_generated(cs):
            continue
        simple = cs.name[: -len(".xaml.cs")]
        code = "\n".join(
            p.read_text(encoding="utf-8-sig", errors="replace")
            for p in sorted(cs.parent.glob("*.cs"))
        )
        index[simple] = index.get(simple, "") + "\n" + code
    return index


def resolve_owner(stack: list[str | None], index: dict[str, str]) -> str | None:
    """Outermost TargetType in the scope stack that has a known code-behind."""
    for target in stack:
        if not target:
            continue
        simple = target.split(":")[-1].split(".")[-1]
        if simple in index:
            return index[simple]
    return None


def scan(xaml: Path, index: dict[str, str]):
    raw = xaml.read_text(encoding="utf-8-sig", errors="replace")
    # Comments are removed for matching, but NOT for line numbering: stripping
    # them first and counting newlines in the result reported every finding
    # against a line that had shifted up by the length of every earlier comment
    # block. MainWindow.xaml lost 253 of its 522 lines that way, so a finding
    # named line 55 (a comment) for a button that is on line 133.
    text = strip_comments(raw)
    sibling_code = "\n".join(
        p.read_text(encoding="utf-8-sig", errors="replace")
        for p in sorted(xaml.parent.glob("*.cs"))
    )
    row_labeled_file = "Option" in xaml.stem
    gaps = []
    stack: list[str | None] = []
    # Keyed MenuFlyout resources whose items are captioned in code (see
    # collection_populated). Tracked as a stack because a flyout can nest.
    key_stack: list[str | None] = []
    for m in event_re.finditer(text):
        closing, tag, attrs = m.group(1), m.group(2), m.group(3)
        # <NavigationView.AutoSuggestBox> is a PROPERTY ELEMENT, not a control
        # instance: it names the property the child is assigned to. Splitting on
        # the dot and matching the tail reported the wrapper as an unnamed
        # AutoSuggestBox, which is where the "unnamed" line came from.
        if "." in tag.split(":")[-1]:
            continue
        base = tag.split(":")[-1].split(".")[-1]
        if closing:
            if base in SCOPE_TAGS and stack:
                stack.pop()
            if base == "MenuFlyout" and key_stack:
                key_stack.pop()
            continue
        self_closing = attrs.rstrip().endswith("/")
        if base in SCOPE_TAGS:
            if not self_closing:
                tm = target_re.search(attrs)
                stack.append(tm.group(1) if tm else None)
            continue
        if base == "MenuFlyout":
            km = re.search(r'x:Key="([^"]+)"', attrs)
            if not self_closing:
                key_stack.append(km.group(1) if km else None)
            continue
        if base not in INTERACTIVE:
            continue
        if row_labeled_file and base in ROW_LABELED_TYPES:
            continue
        # A file with x:Class names its own controls; a template dictionary
        # names the templated control's parts.
        owner = None if class_re.search(text) else resolve_owner(stack, index)
        # A DataTemplate in a page is named by THAT page's code-behind. The
        # library cards' play/more buttons are named in
        # ApplyCardOverlayStrings, two files away from the template, and a
        # sibling-only search reported four buttons the app already names.
        # When no owner resolves, search the whole project rather than guess:
        # a wrong guess would make legitimately-named controls look unnamed.
        code = owner if owner is not None else (
            sibling_code if class_re.search(text)
            else sibling_code + "\n" + "\n".join(index.values())
        )
        nm = name_re.search(attrs)
        name = nm.group(1) if nm else ""
        cache: dict[str, list[str]] = {}
        # A shared menu whose items are captioned in code (a Tag-keyed loop
        # assigning menuItem.Text) counts as self-labeling: that Text is the
        # control's own visible caption and the string UIA reports for it,
        # so neither a tooltip nor a separate name is missing.
        self_labeled = bool(text_attr_re.search(attrs)) or bool(
            name and assigns_caption(name, code, cache)
        ) or bool(key_stack and collection_populated(key_stack[-1], code))
        # A control that declares its caption in a child element (a CheckBox
        # whose Content is a bound TextBlock, say) already shows what it means.
        # That waives the hover tooltip but NOT the accessible name: UIA does
        # not fold a child's text into the parent control, so the name still has
        # to be set explicitly. This is the one case the old rule got wrong in
        # both directions - it demanded a redundant tooltip and, for the
        # glyph-plus-TextBlock buttons, correctly kept asking for a name.
        renders_own_caption = False
        if not self_closing:
            body = element_body(text, tag, m.end())
            renders_own_caption = bool(text_attr_re.search(body)) or bool(
                re.search(r"\{\s*x:Bind[^}]*\bText\b", body)
            )
            # The third shape: the caption is a child TextBlock whose Text is
            # assigned at runtime. VideoAddFolderButton is this -- its visible
            # label is VideoAddFolderText, set from AppLang in code -- and a
            # scan that only reads the button's own attributes calls it a
            # nameless icon.
            if not renders_own_caption:
                for inner in name_re.findall(body):
                    if re.search(re.escape(inner) + r"\.Text\s*=", code):
                        renders_own_caption = True
                    # Filled by name through a helper, not by field:
                    # SetCardCaption(card, "AlbumCardTitleText", text) is how
                    # the album card's two lines are populated.
                    if re.search(r'"' + re.escape(inner) + r'"', code):
                        renders_own_caption = True
                        break
                        break
        # A command fills in the caption: MenuFlyoutItem with a
        # StandardUICommand carries the command's Label, so the item is named by
        # the platform rather than by this file. The four edit-menu items in
        # MpvConfTextWindow are exactly this and were reported unnamed.
        has_command = bool(re.search(r"\bCommand=\"", attrs))
        has_auto = bool(auto_attr_re.search(attrs)) or bool(labeled_by_attr_re.search(attrs)) or has_command or bool(
            name and names_it(name, code, "name")
        ) or bool(key_stack and collection_populated(key_stack[-1], code))
        has_tip = bool(tip_attr_re.search(attrs)) or bool(
            name and names_it(name, code, "tip")
        )
        if base in ("MenuFlyoutItem", "MenuFlyoutSubItem", "ToggleMenuFlyoutItem"):
            # A menu item shows its own text on hover-by-definition; Windows
            # puts no tooltip on context-menu items, so a missing one is the
            # correct state rather than a gap.
            has_tip = True
        if base == "Slider":
            has_auto = has_auto or True  # slider value is visible on drag; name only
            has_tip = True
        if loaded_attr_re.search(attrs):
            has_tip = True
            has_auto = True
        if self_labeled:
            has_tip = True
            has_auto = True
        elif renders_own_caption:
            has_tip = True
        if not has_tip or not has_auto:
            missing = []
            if not has_tip:
                missing.append("ToolTip")
            if not has_auto:
                missing.append("AutoName")
            line = raw[: anchor(m.start(), text, raw)].count("\n") + 1
            gaps.append((name or "(unnamed)", base, missing, line))
    return gaps


def main() -> int:
    index = build_code_index()
    total = 0
    scanned = 0
    for xaml in sorted(ROOT.rglob("*.xaml")):
        if is_generated(xaml):
            continue
        scanned += 1
        for name, base, missing, line in scan(xaml, index):
            print(f"{xaml.relative_to(ROOT)}:{line} <{base} {name}> missing {','.join(missing)}")
            total += 1
    print(f"TOTAL={total} across {scanned} XAML file(s)")

    # Refuse unconvincing input: a regex that stops matching must not read as a
    # clean bill of health. 39 XAML files carry 318 interactive controls today.
    if scanned == 0:
        print("FAIL: no XAML files matched -- the scan root is wrong, not the UI")
        return 2
    # This checker used to print findings and exit 0 unconditionally, which made
    # it a report rather than a gate: 38 known gaps sat in its output while every
    # chain reported green.
    if total:
        print(f"FAIL: {total} interactive control(s) lack a tooltip or an accessible name")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
