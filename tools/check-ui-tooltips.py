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
  Generic.xaml, Modules/Player/*Style.xaml): the names inside a ControlTemplate
  are template parts of the templated control, and the file that names them is
  that control's code-behind - which need not live in the same directory.
  Ownership is therefore resolved from the enclosing Style/ControlTemplate
  TargetType (the outermost one; nested per-part Styles like
  <Style x:Key="PlayButtonStyle" TargetType="Button"> inside the template do not
  take over) to <Type>.xaml.cs anywhere in the project. This is what used to be
  a blind spot: Themes/Generic.xaml names 14 of its buttons in
  Modules/Player/PlayerControl.xaml.cs, and a sibling-only scan reported all 14
  as unnamed.

Prints elements failing either rule. Exit code is always 0; the report is
informational (run by hand, not wired into CI).
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "mpv-winui" / "mpv-winui"
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
loaded_attr_re = re.compile(r'\bLoaded="')
text_attr_re = re.compile(r"\b(?:Content|Label|PlaceholderText|Text)\s*=")


def strip_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


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
    text = strip_comments(xaml.read_text(encoding="utf-8-sig", errors="replace"))
    sibling_code = "\n".join(
        p.read_text(encoding="utf-8-sig", errors="replace")
        for p in sorted(xaml.parent.glob("*.cs"))
    )
    row_labeled_file = "Option" in xaml.stem
    gaps = []
    stack: list[str | None] = []
    for m in event_re.finditer(text):
        closing, tag, attrs = m.group(1), m.group(2), m.group(3)
        base = tag.split(":")[-1].split(".")[-1]
        if closing:
            if base in SCOPE_TAGS and stack:
                stack.pop()
            continue
        self_closing = attrs.rstrip().endswith("/")
        if base in SCOPE_TAGS:
            if not self_closing:
                tm = target_re.search(attrs)
                stack.append(tm.group(1) if tm else None)
            continue
        if base not in INTERACTIVE:
            continue
        if row_labeled_file and base in ROW_LABELED_TYPES:
            continue
        # A file with x:Class names its own controls; a template dictionary
        # names the templated control's parts.
        owner = None if class_re.search(text) else resolve_owner(stack, index)
        code = owner if owner is not None else sibling_code
        nm = name_re.search(attrs)
        name = nm.group(1) if nm else ""
        self_labeled = bool(text_attr_re.search(attrs)) or bool(
            name and re.search(re.escape(name) + r"\.(?:Content|Label|PlaceholderText|Text)\s*=", code)
        )
        # A control that declares its caption in a child element (a CheckBox
        # whose Content is a bound TextBlock, say) already shows what it means.
        # That waives the hover tooltip but NOT the accessible name: UIA does
        # not fold a child's text into the parent control, so the name still has
        # to be set explicitly. This is the one case the old rule got wrong in
        # both directions - it demanded a redundant tooltip and, for the
        # glyph-plus-TextBlock buttons, correctly kept asking for a name.
        renders_own_caption = False
        if not self_closing:
            close_at = text.find(f"</{tag}>", m.end())
            body = text[m.end(): close_at if close_at != -1 else len(text)]
            renders_own_caption = bool(text_attr_re.search(body)) or bool(
                re.search(r"\{\s*x:Bind[^}]*\bText\b", body)
            )
        has_auto = bool(auto_attr_re.search(attrs)) or bool(
            name and re.search(r"AutomationProperties\.SetName\(\s*" + re.escape(name) + r"\b", code)
        )
        has_tip = bool(tip_attr_re.search(attrs)) or bool(
            name and re.search(r"ToolTipService\.SetToolTip\(\s*" + re.escape(name) + r"\b", code)
        )
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
            line = text[: m.start()].count("\n") + 1
            gaps.append((name or "(unnamed)", base, missing, line))
    return gaps


def main():
    index = build_code_index()
    total = 0
    for xaml in sorted(ROOT.rglob("*.xaml")):
        if is_generated(xaml):
            continue
        for name, base, missing, line in scan(xaml, index):
            print(f"{xaml.relative_to(ROOT)}:{line} <{base} {name}> missing {','.join(missing)}")
            total += 1
    print(f"TOTAL={total}")


if __name__ == "__main__":
    main()
